using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using SyncBingo.Domain.Content;

namespace SyncBingo.Domain.Tests.Content;

/// <summary>Rules that hold for any set FsCheck can think of, not just the examples above.</summary>
public sealed class ContentSetPropertyTests
{
    [Property(MaxTest = 200)]
    public void Any_accepted_set_survives_its_own_file(NonEmptyString[] words)
    {
        var entries = words.Select((word, i) => new ContentEntry($"{i} {word.Get}", $"s{i}"));

        var set = ContentSet.TryCreate("prop", entries);
        if (!set.IsSuccess)
        {
            return; // most random input is refused; the property is about what's accepted
        }

        var back = ContentSetFile.Read(ContentSetFile.Write(set.Value));
        back.IsSuccess.ShouldBeTrue(back.Error?.Message);
        back.Value.Pairs.ShouldBe(set.Value.Pairs);
    }

    [Property(MaxTest = 200)]
    public void An_accepted_set_never_repeats_a_ball_or_a_square(PositiveInt seed)
    {
        // 24–60 entries drawn from a small vocabulary, so repeats are common.
        var random = new Random(seed.Get);
        var vocabulary = new[] { "cat", "Cat", "dog", "DOG", "sun", "moon", "star", "tree", "leaf", "rain" };
        var entries = Enumerable.Range(0, random.Next(24, 61)).Select(i =>
            new ContentEntry($"{vocabulary[random.Next(vocabulary.Length)]} {random.Next(40)}", random.Next(3) == 0 ? null : $"{random.Next(80)}"));

        var set = ContentSet.TryCreate("prop", entries);

        if (set.IsSuccess)
        {
            set.Value.Pairs.Select(p => ContentText.Key(p.Ball)).ShouldBeUnique();
            set.Value.Pairs.Select(p => ContentText.Key(p.Square)).ShouldBeUnique();
        }
        else
        {
            set.Error!.Message.ShouldContain(" is on two ");
        }
    }

    [Property(MaxTest = 200)]
    public Property Tidying_is_idempotent() =>
        Prop.ForAll(ArbMap.Default.ArbFor<string>(), text => ContentText.Tidy(ContentText.Tidy(text)) == ContentText.Tidy(text));
}
