alter table updates drop constraint updates_pkey;
alter table updates add constraint updates_unique unique (timestamp, hash, source_id);

alter table updates add column update_id uuid not null default gen_random_uuid();
alter table updates add primary key (update_id);