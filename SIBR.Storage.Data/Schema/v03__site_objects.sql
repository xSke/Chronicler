create table binary_objects (
    hash uuid not null primary key,
    data bytea not null
);

insert into binary_objects (hash, data)
select distinct on (hash) hash, data from site_updates
on conflict do nothing;

alter table site_updates drop column data;