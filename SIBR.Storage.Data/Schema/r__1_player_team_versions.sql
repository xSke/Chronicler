drop materialized view if exists players cascade;
drop materialized view if exists player_versions cascade;

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