drop view if exists players_view;
drop view if exists teams_view;
drop materialized view if exists player_versions;
drop materialized view if exists team_versions;
drop view if exists player_versions_view;
drop view if exists team_versions_view;

create view player_versions_view as
with versions as (
    select player_id,
           timestamp,
           last_timestamp,
           (select data from objects where objects.hash = player_updates.hash) as data
    from (
             select timestamp,
                    hash,
                    player_id,
                    lag(hash) over (partition by player_id order by timestamp)      as last_hash,
                    lag(timestamp) over (partition by player_id order by timestamp) as last_timestamp
             from player_updates
         ) as player_updates
    where (last_hash is null or last_hash != hash))
select player_id,
       timestamp as first_seen,
       coalesce(last_timestamp, (
           select max(timestamp)
           from player_updates
           where player_updates.player_id = versions.player_id
       ))        as last_seen,
       data
from versions;

create materialized view player_versions as
select *
from player_versions_view;
create unique index on player_versions (player_id, first_seen);

create view players_view as
select player_id, timestamp, data
from players
         inner join lateral ( select timestamp, hash
                              from player_updates
                              where players.player_id = player_updates.player_id
                              order by timestamp desc
                              limit 1) as update on true
         inner join objects using (hash);


create view teams_view as
select team_id, timestamp, data
from (select distinct team_id from team_updates) as teams
         inner join lateral ( select timestamp, hash
                              from team_updates
                              where teams.team_id = team_updates.team_id
                              order by timestamp desc
                              limit 1) as update on true
         inner join objects using (hash);

create view team_versions_view as
select team_id,
       timestamp,
       (select data from objects where objects.hash = team_updates.hash) as data
from (
         select timestamp,
                hash,
                team_id,
                lag(hash) over (partition by team_id order by timestamp) as last_hash
         from team_updates
     ) as team_updates
where (last_hash is null or last_hash != hash);