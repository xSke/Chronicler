drop view if exists players_view;
drop materialized view if exists players;
drop materialized view if exists player_versions;
drop materialized view if exists current_roster;
drop materialized view if exists roster_versions;
drop materialized view if exists team_versions;
drop materialized view if exists teams;

create materialized view team_versions as
    select
        update_id, entity_id as team_id, version, first_seen, last_seen, hash, data
    from versions_view
    where type = 2; -- Team = 2
create unique index team_versions_pkey on team_versions (team_id, first_seen);

create materialized view teams as
    select
        team_id, update_id, timestamp, data
    from (select distinct entity_id as team_id from updates where type = 2) as team_ids
        inner join lateral (select * from updates where entity_id = team_ids.team_id order by timestamp desc limit 1) 
            as updates on true
        inner join objects using (hash)
    where type = 2
    order by entity_id, timestamp desc;
create unique index teams_pkey on teams (team_id);

create materialized view current_roster as
    select
        player_id, team_id, position, roster_index
    from teams, unnest(array ['lineup', 'rotation', 'bullpen', 'bench']) as position
        inner join lateral (
            select
                value::uuid as player_id,
                (ordinality - 1) as roster_index
            from jsonb_array_elements_text(data -> position) with ordinality
        ) as players on true;
create unique index on current_roster (player_id);

create materialized view player_versions as
    select
        entity_id as player_id,
        update_id,
        first_seen,
        last_seen,
        hash,
        data
    from versions_view
    where type = 1; -- Player = 1
create unique index player_versions_pkey on player_versions (player_id, first_seen);

create materialized view players as
    select
        player_ids.player_id
    from (select distinct entity_id as player_id from updates where type = 1) as player_ids;
create unique index players_pkey on players (player_id);

create materialized view roster_versions as
    select 
        player_id, team_id, position, roster_index, first_seen, update_id,
        coalesce(
            lead(prev_last_seen) over w,
            (select max(timestamp) from updates u where u.hash = hash)
        ) as last_seen 
    from (
        select 
            player_id, team_id, position, roster_index, first_seen, last_seen, hash, update_id,
            
            lag(team_id) over w as prev_team_id,
            lag(position) over w as prev_position,
            lag(roster_index) over w as prev_roster_index,
            
            lag(last_seen) over w as prev_last_seen
        from team_versions, unnest(array ['lineup', 'rotation', 'bullpen', 'bench']) as position
            inner join lateral (
                select
                    value::uuid as player_id,
                    (ordinality - 1) as roster_index
                from jsonb_array_elements_text(data -> position) with ordinality
            ) as players on true
        window w as (partition by player_id order by first_seen)
    ) as roster_versions_all
    where (team_id, position, roster_index) is distinct from (prev_team_id, prev_position, prev_roster_index)
    window w as (partition by player_id order by first_seen);
create unique index on roster_versions(player_id, first_seen, update_id);

create view players_view as
    select
        p.player_id,
        latest_version.update_id,
        latest_version.first_seen as timestamp,
        data,
        case when not (data->>'deceased')::bool then team_id end as team_id,
        case when not (data->>'deceased')::bool then position end as position,
        case when not (data->>'deceased')::bool then roster_index end as roster_index,
        (not (data->>'deceased')::bool and not exists(
            select 1 from roster_versions rv where rv.player_id = p.player_id and (rv.position = 'lineup' or rv.position = 'rotation') 
        )) as is_forbidden
    from players p
        left join lateral (
            select team_id, position, roster_index from roster_versions rv where rv.player_id = p.player_id order by first_seen desc limit 1
        ) as current_roster on true
        inner join lateral (
            select update_id, first_seen, data from player_versions pv where pv.player_id = p.player_id order by first_seen desc limit 1
        ) as latest_version on true;