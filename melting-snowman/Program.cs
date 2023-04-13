using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // options.SwaggerDoc("melting-snowman", new()
    // {
    //     Title = "Melting Snowman",
    //     Version = "v0.1",
    //     Description = "This is an API for the popular game Melting Snowman (aka. Hangman)."
    // });
    options.SupportNonNullableReferenceTypes();
});

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

var games = new ConcurrentDictionary<int, Game>();
int lastId = 0;

app.MapGet("/game/{id}", (int id) =>
{
    if (!games.TryGetValue(id, out var game))
    {
        return Results.NotFound();
    }

    return Results.Ok(new GameInfoDto(game.MeltingSnowmanGame.Word, game.Guesses));
})
.WithOpenApi(options => new(options)
{
    Summary = "get game information",
    Description = "This route gives you information about the specified game."
})
.Produces<GameInfoDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithTags("Routes");

app.MapPost("/game", () =>
{
    var id = Interlocked.Increment(ref lastId);

    if (!games.TryAdd(id, new Game()))
    {
        // should never happen
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }

    return Results.Ok(new GameIdDto(id));
})
.WithOpenApi(options =>
{
    options.Description = "This route creates a new game so you can use the two other routes with the newly generated ID.";
    options.Summary = "creates a new game";
    return options;
})
.Produces<GameIdDto>(StatusCodes.Status200OK)
.WithTags("Routes");

app.MapPost("/game/{id}", Results<Ok<GuessDto>, NotFound> (int id, [FromBody] string letter) =>
{
    if (!games.TryGetValue(id, out var game))
    {
        return TypedResults.NotFound();
    }

    var occurences = game.MeltingSnowmanGame.Guess(letter);
    game.Guesses += 1;

    return TypedResults.Ok(new GuessDto(occurences, game.MeltingSnowmanGame.Word, game.Guesses));
})
.WithOpenApi(options => new(options)
{
    Summary = "guess a letter",
    Description = "This route lets you guess a letter to find the hidden word."
})
.WithTags("Routes");

app.Run();

class Game
{
    public MeltingSnowmanGame MeltingSnowmanGame { get; }
    public int Guesses { get; set; }

    public Game()
    {
        this.MeltingSnowmanGame = new();
        this.Guesses = 0;
    }
}

record GameInfoDto(string? WordToGuess, int NumberOfGuesses);
record GuessDto(int Occurences, string WordToGuess, int NumberOfGuesses);
record GameIdDto
{
    /// the ID of the newly generated game
    public int Id { get; set; }

    public GameIdDto(int id)
    {
        this.Id = id;
    }
}
