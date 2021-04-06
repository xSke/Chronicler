create table versions (
    version_id uuid not null primary key,
    entity_id uuid not null,
    hash uuid not null,
    valid_from timestamptz not null,
    valid_to timestamptz,
    seq integer not null,
    type smallint not null
);

-- Need the (valid_from, entity_id) pairing for the two-component page token
-- TODO: do we need both of these for the queries we do? intuition says yes, practice might say no
create index versions_type_idx on versions(type, valid_from, entity_id);
create index versions_type_id_idx on versions(type, entity_id, valid_from);

-- Used to speed up grabbing the latest version of things
-- TODO: may not need this if we never explicitly check for null? idk
create index versions_type_id_latest_idx on versions(type, entity_id) where valid_to is null;
