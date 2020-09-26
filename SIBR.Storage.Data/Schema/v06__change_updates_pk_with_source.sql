alter table updates drop constraint updates_pkey;
alter table updates add primary key (timestamp, hash, source_id);