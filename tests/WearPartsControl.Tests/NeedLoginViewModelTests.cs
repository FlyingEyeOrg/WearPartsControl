using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

[Collection(LocalizationSensitiveTestCollection.Name)]
public sealed class NeedLoginViewModelTests
{
    [Fact]
    public void Ctor_WhenCultureIsZhCn_ShouldExposeChineseTexts()
    {
        using var _ = new TestCultureScope("zh-CN");

        var viewModel = new NeedLoginViewModel();

        Assert.Equal("请先登录", viewModel.Title);
        Assert.Equal("当前页面需要登录后才能查看或操作。", viewModel.Description);
        Assert.Equal("请点击右上角登录入口完成登录，然后重新进入当前页面。", viewModel.Hint);
    }

    [Fact]
    public void Ctor_WhenCultureIsEnUs_ShouldExposeEnglishTexts()
    {
        using var _ = new TestCultureScope("en-US");

        var viewModel = new NeedLoginViewModel();

        Assert.Equal("Login Required", viewModel.Title);
        Assert.Equal("You need to log in before viewing or operating on this page.", viewModel.Description);
        Assert.Equal("Use the login entry in the top-right corner, then return to the current page.", viewModel.Hint);
    }
}