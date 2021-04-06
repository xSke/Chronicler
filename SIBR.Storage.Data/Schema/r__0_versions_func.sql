create or replace function append_version(type smallint, entity_id uuid, hash uuid, stamp timestamptz, update_id uuid) returns integer as $$
declare
    last_version record;
begin
    -- get latest version
    select v.hash, v.valid_from, v.seq from versions v
        where v.type = append_version.type and v.entity_id = append_version.entity_id and valid_to is null
        into last_version;
    
    if found then
        if last_version.hash = append_version.hash or last_version.valid_from > stamp then
            -- this is the same as the latest version, quit
            return 0;
        end if;
        
        -- patch the last saved version with end time
        update versions v
            set valid_to = append_version.stamp
            where v.type = append_version.type and v.entity_id = append_version.entity_id and v.valid_to is null;
    end if;
    
    -- insert new version row
    insert into versions (version_id, type, entity_id, hash, valid_from, valid_to, seq)
        values (
            append_version.update_id,
            append_version.type,
            append_version.entity_id,
            append_version.hash,
            append_version.stamp,
            null,
            coalesce(last_version.seq + 1, 0)
        );
    return 1;
end;
$$ language plpgsql;

create or replace function rebuild_entity(type smallint, entity_id uuid) returns integer as $$
declare
    row record;             -- temp var
    last_update record;     -- last seen UPDATE that's the START of a new VERSION
    num_versions int;       -- counter
begin
    -- clear this entity to start from scratch
    delete from versions v 
        where v.type = rebuild_entity.type and v.entity_id = rebuild_entity.entity_id;
    
    num_versions := 0;
    last_update := null;
    
    for row in 
        select hash, timestamp, update_id from updates u
            where u.type = rebuild_entity.type and u.entity_id = rebuild_entity.entity_id
            order by timestamp
    loop
        if last_update is null then
            last_update := row;
        elsif last_update.hash is distinct from row.hash then        
            -- when we detect a new hash, insert the *previous* hash's version
            -- staying one version behind so we have both the start/end (no need for an extra query)
            insert into versions (version_id, type, entity_id, hash, valid_from, valid_to, seq) 
                select
                    last_update.update_id,
                    rebuild_entity.type, 
                    rebuild_entity.entity_id,
                    last_update.hash, 
                    last_update.timestamp,
                    row.timestamp,
                    num_versions;
            
            last_update := row;
            num_versions := num_versions + 1;
        end if;
    end loop;
    
    -- insert the latest version that would've been missed by the above loop
    if last_update is not null then
        insert into versions (version_id, type, entity_id, hash, valid_from, valid_to, seq) 
            select
                last_update.update_id,
                rebuild_entity.type,
                rebuild_entity.entity_id,
                last_update.hash,
                last_update.timestamp,
                null,
                num_versions;
        num_versions := num_versions + 1;
    end if;
    
    return num_versions;
end;
$$ language plpgsql;