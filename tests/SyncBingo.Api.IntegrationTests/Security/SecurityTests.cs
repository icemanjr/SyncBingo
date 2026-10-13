using SyncBingo.Infrastructure.Security;

namespace SyncBingo.Api.IntegrationTests.Security;

/// <summary>Password hashing and session tokens (docs/04 §5, ADR 0005).</summary>
public sealed class SecurityTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new(iterations: 1_000);

    [Fact]
    public void The_right_password_verifies_and_a_wrong_one_does_not()
    {
        var hash = _hasher.Hash("Bingo night!");

        _hasher.Verify("Bingo night!", hash).ShouldBeTrue();
        _hasher.Verify("bingo night!", hash).ShouldBeFalse();
        _hasher.Verify("", hash).ShouldBeFalse();
    }

    [Fact]
    public void Each_hash_has_its_own_salt_and_never_contains_the_password()
    {
        var first = _hasher.Hash("secret");
        var second = _hasher.Hash("secret");

        first.ShouldNotBe(second);
        first.ShouldStartWith("pbkdf2-sha256$1000$");
        first.ShouldNotContain("secret");
    }

    [Fact]
    public void A_hash_keeps_verifying_after_the_cost_is_raised()
    {
        var hash = _hasher.Hash("secret");

        new Pbkdf2PasswordHasher(iterations: 5_000).Verify("secret", hash).ShouldBeTrue("the stored iteration count is used");
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain-text")]
    [InlineData("md5$1$abc$def")]
    [InlineData("pbkdf2-sha256$0$AAAA$AAAA")]
    [InlineData("pbkdf2-sha256$1000$not base64$AAAA")]
    public void A_malformed_hash_never_verifies(string hash)
    {
        _hasher.Verify("secret", hash).ShouldBeFalse();
    }

    [Fact]
    public void The_default_cost_is_the_documented_one()
    {
        new Pbkdf2PasswordHasher().Hash("secret").ShouldStartWith("pbkdf2-sha256$100000$");
    }

    [Fact]
    public void Tokens_are_256_bit_url_safe_and_unique()
    {
        var tokens = new RandomTokenService();

        var issued = Enumerable.Range(0, 100).Select(_ => tokens.Issue()).ToList();

        issued.Select(t => t.Token).Distinct().Count().ShouldBe(100);
        issued.ShouldAllBe(t => t.Token.Length == 43 && t.Token.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_'));
    }

    [Fact]
    public void Only_a_tokens_hash_is_kept_and_it_is_repeatable()
    {
        var tokens = new RandomTokenService();
        var issued = tokens.Issue();

        issued.Hash.ShouldBe(tokens.Hash(issued.Token));
        issued.Hash.Length.ShouldBe(64);
        issued.Hash.ShouldNotContain(issued.Token);
    }
}
