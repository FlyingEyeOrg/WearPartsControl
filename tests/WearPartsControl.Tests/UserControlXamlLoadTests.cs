using System;
using System.Runtime.CompilerServices;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.UserControls;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

[Collection(UserTabControlTestCollection.Name)]
public sealed class UserControlXamlLoadTests
{
    [Fact]
    public void ReplacePartUserControl_ShouldLoadWithoutXamlParseException()
    {
        WpfTestHost.Run(() =>
        {
            var control = new ReplacePartUserControl(
                CreateUninitialized<ReplacePartViewModel>(),
                new StubAutoLogoutInteractionService());

            Assert.NotNull(control);
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void PartManagementUserControl_ShouldLoadWithoutXamlParseException()
    {
        WpfTestHost.Run(() =>
        {
            var control = new PartManagementUserControl(
                CreateUninitialized<PartManagementViewModel>(),
                new StubServiceProvider(),
                new StubAutoLogoutInteractionService());

            Assert.NotNull(control);
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void PartUpdateRecordUserControl_ShouldLoadWithoutXamlParseException()
    {
        WpfTestHost.Run(() =>
        {
            var control = new PartUpdateRecordUserControl(
                CreateUninitialized<PartUpdateRecordViewModel>(),
                new StubAutoLogoutInteractionService());

            Assert.NotNull(control);
        }, ensureApplicationResources: true);
    }

    private static T CreateUninitialized<T>() where T : class
    {
        return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    }

    private sealed class StubAutoLogoutInteractionService : IAutoLogoutInteractionService
    {
        public void NotifyActivity()
        {
        }

        public TResult RunModal<TResult>(Func<TResult> interaction)
        {
            return interaction();
        }
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}