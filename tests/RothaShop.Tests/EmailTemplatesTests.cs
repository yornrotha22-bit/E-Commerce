using RothaShop.Services;

namespace RothaShop.Tests;

public class EmailTemplatesTests
{
    [Fact]
    public void PasswordReset_SubjectMentionsTheStore()
    {
        var content = EmailTemplates.PasswordReset("KShop", "Dara", "https://shop.test/Account/ResetPassword?x=1");

        Assert.Equal("KShop password reset", content.Subject);
    }

    [Fact]
    public void PasswordReset_HtmlContainsTheEncodedLink()
    {
        var link = "https://shop.test/Account/ResetPassword?email=a%40b.test&token=abc+def";

        var content = EmailTemplates.PasswordReset("KShop", "Dara", link);

        Assert.Contains("https://shop.test/Account/ResetPassword?email=a%40b.test&amp;token=abc+def", content.HtmlBody);
        Assert.Contains(link, content.TextBody);
    }

    [Fact]
    public void PasswordReset_EncodesUserSuppliedNames()
    {
        var content = EmailTemplates.PasswordReset(
            "KShop <Ltd>",
            "<script>alert(1)</script>",
            "https://shop.test/reset");

        Assert.DoesNotContain("<script>", content.HtmlBody);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", content.HtmlBody);
        Assert.Contains("KShop &lt;Ltd&gt;", content.HtmlBody);

        // The plain-text body is not HTML, so it keeps the original name.
        Assert.Contains("<script>alert(1)</script>", content.TextBody);
    }

    [Fact]
    public void PasswordReset_UsesFallbacksForBlankValues()
    {
        var content = EmailTemplates.PasswordReset("", "", "https://shop.test/reset");

        Assert.Equal("KShop password reset", content.Subject);
        Assert.Contains("Hello there", content.TextBody);
        Assert.Contains("Hello there", content.HtmlBody);
    }
}
