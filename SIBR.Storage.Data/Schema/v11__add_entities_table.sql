create table entities (
    type smallint,
    entity_id uuid,

    primary key (type, entity_id)
);

create or replace function on_updates_insert_entities_trg() returns trigger as 
$$
    begin
        insert into entities (type, entity_id)
            values (new.type, new.entity_id)
            on conflict do nothing;
        return new;
    end;
$$ language plpgsql;

create trigger updates_insert_entity_trg after insert on updates
    for each row execute procedure on_updates_insert_entities_trg();
    
insert into entities (type, entity_id)
    select distinct type, entity_id from updates
    on conflict do nothing;