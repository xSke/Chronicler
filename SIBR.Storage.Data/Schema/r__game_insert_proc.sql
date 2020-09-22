drop procedure if exists insert_game_updates;

create procedure insert_game_updates(timestamps timestamptz[], game_ids uuid[], hashes uuid[], datas jsonb[]) as
$$
declare
    update record;
begin
    insert into game_updates (timestamp, game_id, hash, data)
    select timestamp, game_id, hash, data
    from (select unnest(timestamps) as timestamp,
                 unnest(game_ids)   as game_id,
                 unnest(hashes)     as hash,
                 unnest(datas)      as data) as update
    on conflict do nothing;

--     for update in select unnest(timestamps) as timestamp,
--                          unnest(hashes)     as hash,
--                          unnest(game_ids)   as game_id,
--                          unnest(datas)      as data
--         loop
--             insert into games (game_id, season, day, game_start, game_end)
--             values (update.game_id,
--                     (update.data ->> 'season')::int,
--                     (update.data ->> 'day')::int,
--                     case
--                         when (update.data ->> 'gameStart')::bool = true then update.timestamp
--                         end,
--                     case
--                         when (update.data ->> 'gameComplete')::bool = true then update.timestamp
--                         end)
--             on conflict (game_id) do update
--                 set game_start = coalesce(least(games.game_start, excluded.game_start), games.game_start),
--                     game_end   = coalesce(least(games.game_end, excluded.game_end), games.game_end);
--         end loop;
end;
$$
    language plpgsql;