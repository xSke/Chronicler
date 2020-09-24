drop materialized view if exists players;
drop materialized view if exists player_versions;
drop materialized view if exists current_roster;
drop materialized view if exists team_versions;
drop materialized view if exists teams;

create materialized view team_versions as
    select
        entity_id as team_id, first_seen, last_seen, hash, data
    from versions_view
    where type = 2; -- Team = 2
create unique index team_versions_pkey on team_versions (team_id, first_seen);

create materialized view teams as
    select distinct on (entity_id)
        entity_id as team_id, timestamp, data
    from updates
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
        first_seen,
        last_seen,
        hash,
        data
    from versions_view
    where type = 1; -- Player = 1
create unique index player_versions_pkey on player_versions (player_id, first_seen);

create materialized view players as
    select distinct on (player_id)
        player_id,
        first_seen as timestamp,
        hash,
        data,
        team_id,
        position,
        roster_index
    from player_versions
         inner join current_roster using (player_id)
    order by player_id, first_seen desc;
create unique index players_pkey on players (player_id);