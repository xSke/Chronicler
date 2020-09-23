drop trigger if exists game_update_insert_trigger on game_updates;

create or replace function on_game_update_insert() returns trigger as
$$
begin
    insert into game_updates_unique (hash, game_id, timestamp, data, season, day, search_tsv)
    select new.hash,
           new.game_id,
           new.timestamp,
           data,
           (data ->> 'season')::int,
           (data ->> 'day')::int,
           to_tsvector('english', data ->> 'lastUpdate')
    from (select data from objects where objects.hash = new.hash) as obj
    on conflict (hash) do update set timestamp = least(game_updates_unique.timestamp, new.timestamp);

    return new;
end;
$$ language plpgsql;

create trigger game_update_insert_trigger
    after insert
    on game_updates
    for each row
execute procedure on_game_update_insert();