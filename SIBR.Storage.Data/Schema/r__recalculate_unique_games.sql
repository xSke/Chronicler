drop function if exists recalculate_game_updates_for_hashes;
create function recalculate_game_updates_for_hashes(hashes uuid[]) returns integer
    language plpgsql
as $$
declare
    count int;
begin
    delete from game_updates_unique gu where gu.hash = any(recalculate_game_updates_for_hashes.hashes);
    insert into game_updates_unique
        select
            hashes.hash,
            game_id,
            timestamp,
            data,
            season,
            day,
            null
        from (select unnest(recalculate_game_updates_for_hashes.hashes) as hash) as hashes
        inner join lateral (select timestamp, game_id, season, day from game_updates gu where gu.hash = hashes.hash order by timestamp limit 1) as first on true
        inner join objects on hashes.hash = objects.hash;
        
    get diagnostics count = row_count;
    return count;
end;
$$;

drop function if exists recalculate_game_updates_for;
drop function if exists recalculate_game_updates_for_game;
create function recalculate_game_updates_for_game(game_ids uuid[]) returns integer
	language plpgsql
as $$
declare
    count int;
begin
    delete from game_updates_unique gu where gu.game_id = any(recalculate_game_updates_for_game.game_ids);
    insert into game_updates_unique
        select
            hashes.hash,
            first.game_id,
            timestamp,
            data,
            season,
            day,
            null
        from (
            select distinct hash from game_updates gu
            where gu.game_id = any(recalculate_game_updates_for_game.game_ids)
        ) hashes
        inner join lateral (
            select * from game_updates gu where gu.hash = hashes.hash order by timestamp limit 1
        ) as first on true
        inner join objects on hashes.hash = objects.hash;
    
    get diagnostics count = row_count;
    return count;
end;
$$;