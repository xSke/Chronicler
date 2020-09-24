drop materialized view if exists temporal_versions;
create materialized view temporal_versions as
    select
        first_seen,
        last_seen,
        hash,
        data
    from versions_view
    where type = 7;
create unique index temporal_versions_pkey on temporal_versions (first_seen);