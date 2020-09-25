drop view if exists games_view;
drop materialized view if exists games;

create materialized view games as
select
    game_id, season, day
from
    (select distinct game_id from game_updates_unique) as game_ids
inner join lateral
    (select season, day from game_updates_unique gu where gu.game_id = game_ids.game_id limit 1) 
        as update on true;
create unique index on games(game_id);
create index games_season_day_idx on games(season, day, game_id);

drop view if exists games_view;
create view games_view as
    select
        game_id,
        season,
        day,
        coalesce(
            game_time_overrides.start_time,
            (select min(timestamp) from game_updates_unique gu where g.game_id = gu.game_id and (gu.data->>'gameStart')::bool = true)
        ) as start_time,
        coalesce(
            game_time_overrides.start_time,
            (select min(timestamp) from game_updates_unique gu where g.game_id = gu.game_id and (gu.data->>'gameComplete')::bool = true)
         ) as end_time,
        data,
        (jsonb_array_length(data->'outcomes') > 0) as has_outcomes,
        (data->>'gameStart')::bool as has_started,
        (data->>'gameComplete')::bool as has_finished,
        (data->>'homeTeam')::uuid as home_team,
        (data->>'awayTeam')::uuid as away_team,
        (data->>'homePitcher')::uuid as home_pitcher,
        (data->>'awayPitcher')::uuid as away_pitcher,
        (data->>'weather')::int as weather
    from games g
        inner join lateral (select data from game_updates_unique gu where g.game_id = gu.game_id order by timestamp desc limit 1)
            as last_update on true
        left join game_time_overrides using (game_id);