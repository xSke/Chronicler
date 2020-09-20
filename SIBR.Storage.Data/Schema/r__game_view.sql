drop view if exists games_view;
drop view if exists game_updates_view;

create view games_view as
select game_id,
       timestamp,
       data,
       (data ->> 'season')::int as season,
       (data ->> 'day')::int as day
from games
         inner join lateral (select timestamp, data
                             from game_updates
                             where games.game_id = game_updates.game_id
                             order by timestamp desc
                             limit 1 ) as update on true;

create view game_updates_view as
select timestamp,
       game_updates.hash,
       data,
       game_id,
       search_tsv,
       (data ->> 'season')::int as season,
       (data ->> 'day')::int    as day
from game_updates;