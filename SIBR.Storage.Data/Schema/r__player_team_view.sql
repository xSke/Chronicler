drop view if exists players_view;
drop view if exists player_updates_view;
drop view if exists teams_view;

create view players_view as
select player_id, timestamp, data
from players
         inner join lateral ( select timestamp, hash
                              from player_updates
                              where players.player_id = player_updates.player_id
                              order by timestamp desc
                              limit 1) as update on true
         inner join objects using (hash);

create view player_updates_view as
select player_id,
       timestamp,
       (select data from objects where objects.hash = player_updates.hash) as data
from (
         select timestamp,
                hash,
                player_id,
                lag(hash) over (partition by player_id order by timestamp) as last_hash
         from player_updates
     ) as player_updates
where (last_hash is null or last_hash != hash);

create view teams_view as
select team_id, timestamp, data
from (select distinct team_id from team_updates) as teams
         inner join lateral ( select timestamp, hash
                              from team_updates
                              where teams.team_id = team_updates.team_id
                              order by timestamp desc
                              limit 1) as update on true
         inner join objects using (hash);

create view team_updates_view as
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