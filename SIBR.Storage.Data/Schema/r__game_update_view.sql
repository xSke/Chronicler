drop view if exists game_updates_view;

create view game_updates_view as
select timestamp,
       game_updates.hash,
       data,
       game_id,
       search_tsv,
       (data ->> 'season')::int    as season,
       (data ->> 'day')::int       as day,
       (data ->> 'homeTeam')::uuid as home_team,
       (data ->> 'awayTeam')::uuid as away_team
from game_updates
         inner join objects using (hash);

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
from ( select distinct team_id from team_updates ) as teams
         inner join lateral ( select timestamp, hash
                              from team_updates
                              where teams.team_id = team_updates.team_id
                              order by timestamp desc
                              limit 1) as update on true
         inner join objects using (hash);