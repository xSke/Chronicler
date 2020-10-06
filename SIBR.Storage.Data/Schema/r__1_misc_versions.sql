drop materialized view if exists temporal_versions;
create materialized view temporal_versions as
    select
        type,
        update_id,
        first_seen,
        last_seen,
        hash,
        data
    from versions_view
    where type = 7;
create unique index temporal_versions_pkey on temporal_versions (first_seen);

drop materialized view if exists simdata_versions;
create materialized view simdata_versions as
    select
        type,
        update_id,
        first_seen,
        last_seen,
        hash,
        data
    from versions_view
    where type = 9 and first_seen is not null;
create unique index simdata_versions_pkey on simdata_versions (first_seen);

drop materialized view if exists globalevents_versions;
create materialized view globalevents_versions as
    select
        type,
        update_id,
        first_seen,
        last_seen,
        hash,
        data
    from versions_view
    where type = 10;
create unique index globalevents_versions_pkey on globalevents_versions (first_seen);