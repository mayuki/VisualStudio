﻿using System;
using System.ComponentModel.Composition;
using System.Reactive.Linq;
using GitHub.Controllers;
using GitHub.Models;
using GitHub.Services;
using GitHub.UI;
using NSubstitute;
using Xunit;
using UnitTests;
using GitHub.ViewModels;
using ReactiveUI;
using System.Collections.Generic;
using System.Reactive.Subjects;
using GitHub.Primitives;
using System.ComponentModel;
using System.Collections.ObjectModel;
using GitHub.App.Factories;

public class UIControllerTests
{
    public class TheDisposeMethod : TestBaseClass
    {
        [Fact]
        public void WithMultipleCallsDoesNotThrowException()
        {
            var uiProvider = Substitute.For<IUIProvider>();
            var hosts = Substitute.For<IRepositoryHosts>();
            var factory = Substitute.For<IUIFactory>();
            var cm = Substitutes.ConnectionManager;
            var uiController = new UIController(uiProvider, hosts, factory, cm);

            uiController.Dispose();
            uiController.Dispose();
        }
    }

    public class UIControllerTestBase : TestBaseClass
    {
        protected void SetupView<VM>(IExportFactoryProvider factory, GitHub.Exports.UIViewType type)
            where VM : class, IViewModel
        {
            IView view;
            if (type == GitHub.Exports.UIViewType.PRList)
                view = Substitutes.For<IView, IViewFor<VM>, IHasCreationView, IHasDetailView>();
            else
                view = Substitute.For<IView, IViewFor<VM>>();

            view.Done.Returns(new ReplaySubject<object>());
            view.Cancel.Returns(new ReplaySubject<object>());

            (view as IHasDetailView)?.Open.Returns(new ReplaySubject<object>());
            (view as IHasCreationView)?.Create.Returns(new ReplaySubject<object>());

            var e = new ExportLifetimeContext<IView>(view, () => { });
            factory.GetView(type).Returns(e);
        }

        protected void SetupViewModel<VM>(IExportFactoryProvider factory, GitHub.Exports.UIViewType type)
            where VM : class, IViewModel
        {
            var v = Substitute.For<VM, INotifyPropertyChanged>();
            var e = new ExportLifetimeContext<IViewModel>(v, () => { });

            factory.GetViewModel(type).Returns(e);
        }

        protected void RaisePropertyChange(object obj, string prop)
        {
            (obj as INotifyPropertyChanged).PropertyChanged += Raise.Event<PropertyChangedEventHandler>(new PropertyChangedEventArgs(prop));
        }

        protected IUIFactory SetupFactory(IServiceProvider provider)
        {
            var factory = provider.GetExportFactoryProvider();
            SetupViewModel<ILoginControlViewModel>(factory, GitHub.Exports.UIViewType.Login);
            SetupViewModel<ITwoFactorDialogViewModel>(factory, GitHub.Exports.UIViewType.TwoFactor);
            SetupViewModel<IRepositoryCloneViewModel>(factory, GitHub.Exports.UIViewType.Clone);
            SetupViewModel<IRepositoryCreationViewModel>(factory, GitHub.Exports.UIViewType.Create);
            SetupViewModel<IRepositoryPublishViewModel>(factory, GitHub.Exports.UIViewType.Publish);
            SetupViewModel<IPullRequestListViewModel>(factory, GitHub.Exports.UIViewType.PRList);
            SetupViewModel<IPullRequestDetailViewModel>(factory, GitHub.Exports.UIViewType.PRDetail);
            SetupViewModel<IPullRequestCreationViewModel>(factory, GitHub.Exports.UIViewType.PRCreation);

            SetupView<ILoginControlViewModel>(factory, GitHub.Exports.UIViewType.Login);
            SetupView<ITwoFactorDialogViewModel>(factory, GitHub.Exports.UIViewType.TwoFactor);
            SetupView<IRepositoryCloneViewModel>(factory, GitHub.Exports.UIViewType.Clone);
            SetupView<IRepositoryCreationViewModel>(factory, GitHub.Exports.UIViewType.Create);
            SetupView<IRepositoryPublishViewModel>(factory, GitHub.Exports.UIViewType.Publish);
            SetupView<IPullRequestListViewModel>(factory, GitHub.Exports.UIViewType.PRList);
            SetupView<IPullRequestDetailViewModel>(factory, GitHub.Exports.UIViewType.PRDetail);
            SetupView<IPullRequestCreationViewModel>(factory, GitHub.Exports.UIViewType.PRCreation);

            return new UIFactory(factory);
        }

        protected IConnection SetupConnection(IServiceProvider provider, IRepositoryHosts hosts,
            IRepositoryHost host)
        {
            var connection = provider.GetConnection();
            connection.Login().Returns(Observable.Return(connection));
            hosts.LookupHost(connection.HostAddress).Returns(host);
            host.IsLoggedIn.Returns(true);
            return connection;
        }

        protected void TriggerCancel(IView view)
        {
            ((ReplaySubject<object>)view.Cancel).OnNext(null);
        }

        protected void TriggerDone(IView view)
        {
            ((ReplaySubject<object>)view.Done).OnNext(null);
        }
    }

    public class AuthFlow : UIControllerTestBase
    {
        [Fact]
        public void RunningNonAuthFlowWithoutBeingLoggedInRunsAuthFlow()
        {
            var provider = Substitutes.GetFullyMockedServiceProvider();
            var hosts = provider.GetRepositoryHosts();
            var factory = SetupFactory(provider);
            var cm = provider.GetConnectionManager();
            var cons = new ObservableCollection<IConnection>();
            cm.Connections.Returns(cons);

            using (var uiController = new UIController((IUIProvider)provider, hosts, factory, cm))
            {
                var count = 0;
                var flow = uiController.SelectFlow(UIControllerFlow.Clone);
                flow.Subscribe(uc =>
                {
                    switch (++count)
                    {
                        case 1:
                            Assert.IsAssignableFrom<IViewFor<ILoginControlViewModel>>(uc);
                            TriggerCancel(uc);
                            break;
                    }
                });

                uiController.Start(null);
                Assert.Equal(1, count);
                Assert.True(uiController.IsStopped);
            }
        }

        [Fact]
        public void RunningNonAuthFlowWhenLoggedInRunsNonAuthFlow()
        {
            var provider = Substitutes.GetFullyMockedServiceProvider();
            var hosts = provider.GetRepositoryHosts();
            var factory = SetupFactory(provider);
            var cm = provider.GetConnectionManager();
            var cons = new ObservableCollection<IConnection>();
            cm.Connections.Returns(cons);

            // simulate being logged in
            cons.Add(SetupConnection(provider, hosts, hosts.GitHubHost));

            using (var uiController = new UIController((IUIProvider)provider, hosts, factory, cm))
            {
                var count = 0;
                var flow = uiController.SelectFlow(UIControllerFlow.Clone);
                flow.Subscribe(uc =>
                {
                    switch (++count)
                    {
                        case 1:
                            Assert.IsAssignableFrom<IViewFor<IRepositoryCloneViewModel>>(uc);
                            TriggerCancel(uc);
                            break;
                    }
                });

                uiController.Start(null);
                Assert.Equal(1, count);
                Assert.True(uiController.IsStopped);
            }
        }

        [Fact]
        public void RunningAuthFlowWithoutBeingLoggedInRunsAuthFlow()
        {
            var provider = Substitutes.GetFullyMockedServiceProvider();
            var hosts = provider.GetRepositoryHosts();
            var factory = SetupFactory(provider);
            var cm = provider.GetConnectionManager();
            var cons = new ObservableCollection<IConnection>();
            cm.Connections.Returns(cons);

            using (var uiController = new UIController((IUIProvider)provider, hosts, factory, cm))
            {
                var count = 0;
                var flow = uiController.SelectFlow(UIControllerFlow.Authentication);
                flow.Subscribe(uc =>
                {
                    switch (++count)
                    {
                        case 1:
                            Assert.IsAssignableFrom<IViewFor<ILoginControlViewModel>>(uc);
                            TriggerCancel(uc);
                            break;
                    }
                });

                uiController.Start(null);
                Assert.Equal(1, count);
                Assert.True(uiController.IsStopped);
            }
        }

        [Fact]
        public void RunningAuthFlowWhenLoggedInRunsAuthFlow()
        {
            var provider = Substitutes.GetFullyMockedServiceProvider();
            var hosts = provider.GetRepositoryHosts();
            var factory = SetupFactory(provider);
            var cm = provider.GetConnectionManager();

            // simulate being logged in
            var host = hosts.GitHubHost;
            var connection = SetupConnection(provider, hosts, host);
            var cons = new ObservableCollection<IConnection> { connection };
            cm.Connections.Returns(cons);

            using (var uiController = new UIController((IUIProvider)provider, hosts, factory, cm))
            {
                var count = 0;
                var flow = uiController.SelectFlow(UIControllerFlow.Authentication);
                flow.Subscribe(uc =>
                {
                    switch (++count)
                    {
                        case 1:
                            Assert.IsAssignableFrom<IViewFor<ILoginControlViewModel>>(uc);
                            TriggerCancel(uc);
                            break;
                    }
                });

                uiController.Start(null);
                Assert.Equal(1, count);
                Assert.True(uiController.IsStopped);
            }
        }

        [Fact]
        public void AuthFlowWithout2FA()
        {
            var provider = Substitutes.GetFullyMockedServiceProvider();
            var hosts = provider.GetRepositoryHosts();
            var factory = SetupFactory(provider);
            var cm = provider.GetConnectionManager();
            var cons = new ObservableCollection<IConnection>();
            cm.Connections.Returns(cons);

            using (var uiController = new UIController((IUIProvider)provider, hosts, factory, cm))
            {
                var count = 0;
                var flow = uiController.SelectFlow(UIControllerFlow.Clone);
                flow.Subscribe(uc =>
                {
                    switch (++count)
                    {
                        case 1:
                            Assert.IsAssignableFrom<IViewFor<ILoginControlViewModel>>(uc);
                            // login
                            cons.Add(SetupConnection(provider, hosts, hosts.GitHubHost));
                            TriggerDone(uc);
                            break;
                        case 2:
                            Assert.IsAssignableFrom<IViewFor<IRepositoryCloneViewModel>>(uc);
                            TriggerCancel(uc);
                            break;
                    }
                });

                uiController.Start(null);
                Assert.Equal(2, count);
                Assert.True(uiController.IsStopped);
            }
        }

        [Fact]
        public void AuthFlowWith2FA()
        {
            var provider = Substitutes.GetFullyMockedServiceProvider();
            var hosts = provider.GetRepositoryHosts();
            var factory = SetupFactory(provider);
            var cm = provider.GetConnectionManager();
            var cons = new ObservableCollection<IConnection>();
            cm.Connections.Returns(cons);

            using (var uiController = new UIController((IUIProvider)provider, hosts, factory, cm))
            {
                var count = 0;
                var flow = uiController.SelectFlow(UIControllerFlow.Clone);
                flow.Subscribe(uc =>
                {
                    switch (++count)
                    {
                        case 1:
                            Assert.IsAssignableFrom<IViewFor<ILoginControlViewModel>>(uc);
                            var vm = factory.CreateViewAndViewModel(GitHub.Exports.UIViewType.TwoFactor).ViewModel;
                            vm.IsShowing.Returns(true);
                            RaisePropertyChange(vm, "IsShowing");
                            break;
                        case 2:
                            Assert.IsAssignableFrom<IViewFor<ITwoFactorDialogViewModel>>(uc);
                            // login
                            cons.Add(SetupConnection(provider, hosts, hosts.GitHubHost));
                            // continue by triggering done on login view
                            var v = factory.CreateViewAndViewModel(GitHub.Exports.UIViewType.Login).View;
                            TriggerDone(v);
                            break;
                        case 3:
                            Assert.IsAssignableFrom<IViewFor<IRepositoryCloneViewModel>>(uc);
                            TriggerCancel(uc);
                            break;
                    }
                });

                uiController.Start(null);
                Assert.Equal(3, count);
                Assert.True(uiController.IsStopped);
            }
        }

        [Fact]
        public void BackAndForth()
        {
            var provider = Substitutes.GetFullyMockedServiceProvider();
            var hosts = provider.GetRepositoryHosts();
            var factory = SetupFactory(provider);
            var cm = provider.GetConnectionManager();
            var cons = new ObservableCollection<IConnection>();
            cm.Connections.Returns(cons);

            using (var uiController = new UIController((IUIProvider)provider, hosts, factory, cm))
            {
                var count = 0;
                var flow = uiController.SelectFlow(UIControllerFlow.Clone);
                flow.Subscribe(uc =>
                {
                    switch (++count)
                    {
                        case 1: {
                            Assert.IsAssignableFrom<IViewFor<ILoginControlViewModel>>(uc);
                            var vm = factory.CreateViewAndViewModel(GitHub.Exports.UIViewType.TwoFactor).ViewModel;
                            vm.IsShowing.Returns(true);
                            RaisePropertyChange(vm, "IsShowing");
                            break;
                        }
                        case 2: {
                            Assert.IsAssignableFrom<IViewFor<ITwoFactorDialogViewModel>>(uc);
                            var vm = factory.CreateViewAndViewModel(GitHub.Exports.UIViewType.TwoFactor).ViewModel;
                            vm.IsShowing.Returns(false);
                            RaisePropertyChange(vm, "IsShowing");
                            TriggerCancel(uc);
                            break;
                        }
                        case 3: {
                            Assert.IsAssignableFrom<IViewFor<ILoginControlViewModel>>(uc);
                            var vm = factory.CreateViewAndViewModel(GitHub.Exports.UIViewType.TwoFactor).ViewModel;
                            vm.IsShowing.Returns(true);
                            RaisePropertyChange(vm, "IsShowing");
                            break;
                        }
                        case 4: {
                            Assert.IsAssignableFrom<IViewFor<ITwoFactorDialogViewModel>>(uc);
                            // login
                            cons.Add(SetupConnection(provider, hosts, hosts.GitHubHost));
                            var v = factory.CreateViewAndViewModel(GitHub.Exports.UIViewType.Login).View;
                            TriggerDone(v);
                            break;
                        }
                        case 5: {
                            Assert.IsAssignableFrom<IViewFor<IRepositoryCloneViewModel>>(uc);
                            uiController.Stop();
                            break;
                        }
                    }
                });

                uiController.Start(null);
                Assert.Equal(5, count);
                Assert.True(uiController.IsStopped);
            }
        }
    }

    public class CloneFlow : UIControllerTestBase
    {
        [Fact]
        public void Flow()
        {
            var provider = Substitutes.GetFullyMockedServiceProvider();
            var hosts = provider.GetRepositoryHosts();
            var factory = SetupFactory(provider);
            var cm = provider.GetConnectionManager();
            var cons = new ObservableCollection<IConnection>();
            cm.Connections.Returns(cons);

            // simulate being logged in
            cons.Add(SetupConnection(provider, hosts, hosts.GitHubHost));

            using (var uiController = new UIController((IUIProvider)provider, hosts, factory, cm))
            {
                var count = 0;
                var flow = uiController.SelectFlow(UIControllerFlow.Clone);
                flow.Subscribe(uc =>
                {
                    switch (++count)
                    {
                        case 1:
                            Assert.IsAssignableFrom<IViewFor<IRepositoryCloneViewModel>>(uc);
                            TriggerDone(uc);
                            break;
                    }
                });

                uiController.Start(null);
                Assert.Equal(1, count);
                Assert.True(uiController.IsStopped);
            }
        }
    }

    public class CreateFlow : UIControllerTestBase
    {
        [Fact]
        public void Flow()
        {
            var provider = Substitutes.GetFullyMockedServiceProvider();
            var hosts = provider.GetRepositoryHosts();
            var factory = SetupFactory(provider);
            var cm = provider.GetConnectionManager();
            var cons = new ObservableCollection<IConnection>();
            cm.Connections.Returns(cons);

            // simulate being logged in
            cons.Add(SetupConnection(provider, hosts, hosts.GitHubHost));

            using (var uiController = new UIController((IUIProvider)provider, hosts, factory, cm))
            {
                var count = 0;
                var flow = uiController.SelectFlow(UIControllerFlow.Create);
                flow.Subscribe(uc =>
                {
                    switch (++count)
                    {
                        case 1:
                            Assert.IsAssignableFrom<IViewFor<IRepositoryCreationViewModel>>(uc);
                            TriggerDone(uc);
                            break;
                    }
                });

                uiController.Start(null);
                Assert.Equal(1, count);
                Assert.True(uiController.IsStopped);
            }
        }
    }

    public class PublishFlow : UIControllerTestBase
    {
        [Fact]
        public void FlowWithConnection()
        {
            var provider = Substitutes.GetFullyMockedServiceProvider();
            var hosts = provider.GetRepositoryHosts();
            var factory = SetupFactory(provider);
            var cm = provider.GetConnectionManager();
            var cons = new ObservableCollection<IConnection>();
            cm.Connections.Returns(cons);
            var connection = SetupConnection(provider, hosts, hosts.GitHubHost);

            // simulate being logged in
            cons.Add(connection);

            using (var uiController = new UIController((IUIProvider)provider, hosts, factory, cm))
            {
                var count = 0;
                var flow = uiController.SelectFlow(UIControllerFlow.Publish);
                flow.Subscribe(uc =>
                {
                    switch (++count)
                    {
                        case 1:
                            Assert.IsAssignableFrom<IViewFor<IRepositoryPublishViewModel>>(uc);
                            ((IUIProvider)provider).Received().AddService(uiController, connection);
                            TriggerDone(uc);
                            break;
                    }
                });

                uiController.Start(connection);
                Assert.Equal(1, count);
                Assert.True(uiController.IsStopped);
            }
        }

        [Fact]
        public void FlowWithoutConnection()
        {
            var provider = Substitutes.GetFullyMockedServiceProvider();
            var hosts = provider.GetRepositoryHosts();
            var factory = SetupFactory(provider);
            var cm = provider.GetConnectionManager();
            var cons = new ObservableCollection<IConnection>();
            cm.Connections.Returns(cons);
            var connection = SetupConnection(provider, hosts, hosts.GitHubHost);

            // simulate being logged in
            cons.Add(connection);

            using (var uiController = new UIController((IUIProvider)provider, hosts, factory, cm))
            {
                var count = 0;
                var flow = uiController.SelectFlow(UIControllerFlow.Publish);
                flow.Subscribe(uc =>
                {
                    switch (++count)
                    {
                        case 1:
                            Assert.IsAssignableFrom<IViewFor<IRepositoryPublishViewModel>>(uc);
                            ((IUIProvider)provider).Received().AddService(uiController, connection);
                            TriggerDone(uc);
                            break;
                    }
                });

                uiController.Start(null);
                Assert.Equal(1, count);
                Assert.True(uiController.IsStopped);
            }
        }
    }

    public class PullRequestsFlow : UIControllerTestBase
    {
        [Fact]
        public void Flow()
        {
            var provider = Substitutes.GetFullyMockedServiceProvider();
            var hosts = provider.GetRepositoryHosts();
            var factory = SetupFactory(provider);
            var cm = provider.GetConnectionManager();
            var cons = new ObservableCollection<IConnection>();
            cm.Connections.Returns(cons);

            // simulate being logged in
            cons.Add(SetupConnection(provider, hosts, hosts.GitHubHost));

            using (var uiController = new UIController((IUIProvider)provider, hosts, factory, cm))
            {
                var count = 0;
                bool? success = null;
                var flow = uiController.SelectFlow(UIControllerFlow.PullRequests);
                uiController.ListenToCompletionState()
                    .Subscribe(s =>
                    {
                        success = s;
                    });
                flow.Subscribe(uc =>
                {
                    switch (++count)
                    {
                        case 1:
                            Assert.IsAssignableFrom<IViewFor<IPullRequestListViewModel>>(uc);
                            ((ReplaySubject<object>)((IHasDetailView)uc).Open).OnNext(1);
                            break;
                        case 2:
                            Assert.IsAssignableFrom<IViewFor<IPullRequestDetailViewModel>>(uc);
                            TriggerDone(uc);
                            break;
                        case 3:
                            Assert.IsAssignableFrom<IViewFor<IPullRequestListViewModel>>(uc);
                            ((ReplaySubject<object>)((IHasDetailView)uc).Open).OnNext(1);
                            break;
                        case 4:
                            Assert.IsAssignableFrom<IViewFor<IPullRequestDetailViewModel>>(uc);
                            TriggerCancel(uc);
                            break;
                        case 5:
                            Assert.IsAssignableFrom<IViewFor<IPullRequestListViewModel>>(uc);
                            ((ReplaySubject<object>)((IHasCreationView)uc).Create).OnNext(null);
                            break;
                        case 6:
                            Assert.IsAssignableFrom<IViewFor<IPullRequestCreationViewModel>>(uc);
                            TriggerCancel(uc);
                            break;
                        case 7:
                            Assert.IsAssignableFrom<IViewFor<IPullRequestListViewModel>>(uc);
                            ((ReplaySubject<object>)((IHasCreationView)uc).Create).OnNext(null);
                            break;
                        case 8:
                            Assert.IsAssignableFrom<IViewFor<IPullRequestCreationViewModel>>(uc);
                            TriggerDone(uc);
                            break;
                        case 9:
                            Assert.IsAssignableFrom<IViewFor<IPullRequestListViewModel>>(uc);
                            TriggerCancel(uc);
                            break;
                    }
                });

                uiController.Start(null);
                Assert.Equal(9, count);
                Assert.True(uiController.IsStopped);
                Assert.True(success.HasValue);
                Assert.False(success);
            }
        }

        [Fact]
        public void ShuttingDown()
        {
            var provider = Substitutes.GetFullyMockedServiceProvider();
            var hosts = provider.GetRepositoryHosts();
            var factory = SetupFactory(provider);
            var cm = provider.GetConnectionManager();
            var cons = new ObservableCollection<IConnection>();
            cm.Connections.Returns(cons);

            // simulate being logged in
            cons.Add(SetupConnection(provider, hosts, hosts.GitHubHost));

            using (var uiController = new UIController((IUIProvider)provider, hosts, factory, cm))
            {
                var count = 0;
                bool? success = null;
                var flow = uiController.SelectFlow(UIControllerFlow.PullRequests);
                uiController.ListenToCompletionState()
                    .Subscribe(s =>
                    {
                        success = s;
                        Assert.Equal(4, count);
                        count++;
                    });
                flow.Subscribe(uc =>
                {
                    switch (++count)
                    {
                        case 1:
                            Assert.IsAssignableFrom<IViewFor<IPullRequestListViewModel>>(uc);
                            ((ReplaySubject<object>)((IHasDetailView)uc).Open).OnNext(1);
                            break;
                        case 2:
                            Assert.IsAssignableFrom<IViewFor<IPullRequestDetailViewModel>>(uc);
                            TriggerDone(uc);
                            break;
                        case 3:
                            Assert.IsAssignableFrom<IViewFor<IPullRequestListViewModel>>(uc);
                            ((ReplaySubject<object>)((IHasDetailView)uc).Open).OnNext(1);
                            break;
                        case 4:
                            Assert.IsAssignableFrom<IViewFor<IPullRequestDetailViewModel>>(uc);
                            uiController.Stop();
                            break;
                    }
                }, () =>
                {
                    Assert.Equal(5, count);
                    count++;
                });

                uiController.Start(null);
                Assert.Equal(6, count);
                Assert.True(uiController.IsStopped);
                Assert.True(success.HasValue);
                Assert.True(success);
            }
        }
    }
}