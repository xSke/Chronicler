drop view if exists players_view;
drop materialized view if exists players;
drop materialized view if exists player_versions;
drop materialized view if exists current_roster;
drop materialized view if exists roster_versions;
drop materialized view if exists team_versions;
drop materialized view if exists teams;

create materialized view team_versions as
    select
        version_id as update_id,
        entity_id as team_id,
        seq as version,
        valid_from as first_seen,
        valid_to as last_seen,
        hash,
        data
    from versions
    inner join objects using (hash)
    where type = 2; -- Team = 2
create unique index team_versions_pkey on team_versions (team_id, first_seen);

create materialized view teams as
    select
        entity_id as team_id, update_id, timestamp, data
    from latest_view
    where type = 2;
create unique index teams_pkey on teams (team_id);

create materialized view current_roster as
    select
        player_id, team_id, position, roster_index
    from teams, unnest(array ['lineup', 'rotation', 'bullpen', 'bench', 'shadows']) as position
        inner join lateral (
            select
                value::uuid as player_id,
                (ordinality - 1) as roster_index
            from jsonb_array_elements_text(data -> position) with ordinality
        ) as players on true
    -- PODS team is gone from the API so players on there are still listed in historical data
    -- AND the new teams they're also on. Need this to filter them out from "current roster"
    where team_id != '40b9ec2a-cb43-4dbb-b836-5accb62e7c20';
create unique index on current_roster (player_id, team_id);

create materialized view player_versions as
    select
        e.entity_id as player_id,
        v.version_id as update_id,
        v.valid_from as first_seen,
        v.valid_to as last_seen,
        v.hash,
        data
    from entities e
        inner join versions v using (type, entity_id)
        inner join objects using (hash)
        where type = 1;
create unique index player_versions_pkey on player_versions (player_id, first_seen);

create materialized view players as
    select
        entity_id as player_id
    from entities
    where type = 1;
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
        from team_versions, unnest(array ['lineup', 'rotation', 'bullpen', 'bench', 'shadows']) as position
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
-- *sigh* why do you keep breaking my stuff, Lori Boston
create unique index on roster_versions(player_id, team_id, position, first_seen, update_id);

create view players_view as
    select
        l.entity_id as player_id,
        l.update_id,
        l.timestamp as timestamp,
        (select min(first_seen) from player_versions pv where pv.player_id = l.entity_id) as first_seen,
        (select max(last_seen) from player_versions pv where pv.player_id = l.entity_id) as last_seen,
        data,
        case when not (data->>'deceased')::bool then team_id end as team_id,
        case when not (data->>'deceased')::bool then position end as position,
        case when not (data->>'deceased')::bool then roster_index end as roster_index,
        (not (data->>'deceased')::bool and not exists(
            select 1 from roster_versions rv where rv.player_id = l.entity_id and (rv.position = 'lineup' or rv.position = 'rotation') 
        )) as is_forbidden
    from latest_view l
        left join lateral (
            select team_id, position, roster_index from current_roster cr where cr.player_id = l.entity_id limit 1
        ) as current_roster on true
        where l.type = 1;