alter table entities add column last_update uuid;

create or replace function on_updates_insert_entities_trg() returns trigger as 
$$
    begin
        insert into entities (type, entity_id, last_update)
            values (new.type, new.entity_id, new.update_id)
            on conflict do nothing;
            
        update entities e
            set last_update = new.update_id
            where
                e.type = new.type and
                e.entity_id = new.entity_id and
                new.timestamp > (select timestamp from updates u where u.update_id = e.last_update);
                
        return new;
    end;
$$ language plpgsql;

update entities e
    set last_update = (
        select update_id
        from updates u
            where u.type = e.type and u.entity_id = e.entity_id 
        order by timestamp desc limit 1
    )
    where e.last_update is null;
    
alter table entities alter column last_update set not null;