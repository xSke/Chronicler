drop materialized view if exists tributes_hourly;
drop materialized view if exists tributes_by_player;

create materialized view tributes_by_player as
select distinct on (timestamp) timestamp,
       (jsonb_array_elements(data) -> 'peanuts')::int    as peanuts,
       (jsonb_array_elements(data) ->> 'playerId')::uuid as player_id
from updates
         inner join objects using (hash)
where type = 6;
create unique index on tributes_by_player (timestamp, player_id);

create materialized view tributes_hourly as
select
    date_trunc('hour', timestamp) as timestamp,
    player_id,
    min(peanuts) as peanuts
from tributes_by_player
group by date_trunc('hour', timestamp), player_id;
create unique index on tributes_hourly (timestamp, player_id);