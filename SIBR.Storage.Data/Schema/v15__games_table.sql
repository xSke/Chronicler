drop view if exists games_view;
drop materialized view if exists games;

create table games (
    game_id uuid primary key not null,
    season smallint not null,
    tournament smallint not null,
    day smallint not null
);
create index games_season_day_idx on games(season, day, game_id);
create index games_tournament_day_idx on games(tournament, day, game_id);
create index games_season_tournament_day_idx on games(season, tournament, day, game_id);

insert into games (game_id, season, tournament, day)
    select
        distinct on (game_id)
        game_id, season, tournament, day 
    from game_updates_unique;
    
create or replace function game_updates_unique_insert_game_trg_func()
    returns trigger language plpgsql
as $$
begin
    insert into games (game_id, season, tournament, day)
        values (new.game_id, new.season, new.tournament, new.day)
        on conflict do nothing; 
        
    return new;
end;
$$;

create trigger game_updates_unique_insert_game_trg
    after insert on game_updates_unique
    for each row execute procedure game_updates_unique_insert_game_trg_func();