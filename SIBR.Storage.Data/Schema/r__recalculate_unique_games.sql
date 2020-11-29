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
            null,
            null,
            tournament
        from (select unnest(recalculate_game_updates_for_hashes.hashes) as hash) as hashes
        inner join lateral (select timestamp, game_id, season, day, tournament from game_updates gu where gu.hash = hashes.hash order by timestamp limit 1) as first on true
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
            hash,
            game_id,
            min(timestamp) as timestamp,
            (select data from objects o where o.hash = gu.hash) as data,
            min(season) as season,
            min(day) as day,
            null as search_tsv,
            null as play_count,
            min(tournament) as tournament
        from game_updates gu
        where game_id = any(recalculate_game_updates_for_game.game_ids)
        group by game_id, hash;
    
    get diagnostics count = row_count;
    return count;
end;
$$;

drop function if exists recalculate_game_updates_for_season;
create function recalculate_game_updates_for_season(season int) returns integer
	language plpgsql
as $$
declare
    count int;
begin
    delete from game_updates_unique gu where gu.season = recalculate_game_updates_for_season.season;
    insert into game_updates_unique
        select
            hash,
            game_id,
            min(timestamp) as timestamp,
            (select data from objects o where o.hash = gu.hash) as data,
            min(gu.season) as season,
            min(day) as day,
            null as search_tsv,
            null as play_count,
            min(tournament) as tournament
        from game_updates gu
        where gu.season = recalculate_game_updates_for_season.season
        group by game_id, hash;
    
    get diagnostics count = row_count;
    return count;
end;
$$;