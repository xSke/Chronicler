drop function if exists recalculate_game_updates_for(game_id uuid[]);
create or replace function recalculate_game_updates_for(game_id uuid[]) returns int as
$$
declare
    count int;
begin
    delete from game_updates_unique gu where gu.game_id = any(recalculate_game_updates_for.game_id);
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
            where gu.game_id = any(recalculate_game_updates_for.game_id)
        ) hashes
        inner join lateral (
            select * from game_updates gu where gu.hash = hashes.hash order by timestamp limit 1
        ) as first on true
        inner join objects on hashes.hash = objects.hash;
    
    get diagnostics count = row_count;
    return count;
end;
$$ language plpgsql;