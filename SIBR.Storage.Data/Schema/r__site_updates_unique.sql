drop view if exists site_updates_unique;
create view site_updates_unique as
    select
        timestamp, hash, path, 
        octet_length(data) as size
    from (
        select distinct on (hash, path)
            timestamp, hash, path
        from site_updates
        order by hash, path, timestamp
    ) as site_updates
    inner join binary_objects using (hash)