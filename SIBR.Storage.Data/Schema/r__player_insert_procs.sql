drop procedure if exists insert_player_updates;

create procedure insert_player_updates(timestamps timestamptz[], hashes uuid[], player_ids uuid[]) as
$$
declare
    update       record;
    last_version record;
begin
    for update in select unnest(timestamps) as timestamp, unnest(hashes) as hash, unnest(player_ids) as player_id
        loop
            insert into player_updates (timestamp, hash, player_id)
            values (update.timestamp, update.hash, update.player_id)
            on conflict do nothing;

            select *
            from player_versions
            where player_id = update.player_id
              and player_versions.first_seen < update.timestamp
            order by player_versions.first_seen desc
            limit 1
            into last_version;

            if last_version is not null and last_version.hash = update.hash then
                update player_versions
                set first_seen = least(update.timestamp, first_seen),
                    last_seen  = greatest(update.timestamp, last_seen)
                where version_id = last_version.version_id;
            else
                insert into player_versions(player_id, first_seen, last_seen, hash, data)
                select update.player_id,
                       update.timestamp,
                       update.timestamp,
                       update.hash,
                       (select data from objects where objects.hash = update.hash);
            end if;
        end loop;
end;
$$
    language plpgsql;