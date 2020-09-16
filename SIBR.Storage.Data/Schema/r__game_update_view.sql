drop view if exists game_updates_view;

create view game_updates_view as
select timestamp,
       hash,
       data,
       search_tsv,
       coalesce(data ->> '_id', data ->> 'id')::uuid as game_id,
       (data ->> 'season')::int                      as season,
       (data ->> 'day')::int                         as day,
       (data ->> 'homeTeam')::uuid                   as home_team,
       (data ->> 'awayTeam')::uuid                   as away_team
from game_updates;