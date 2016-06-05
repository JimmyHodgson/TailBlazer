using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using TailBlazer.Domain.FileHandling.Search;
using TailBlazer.Domain.Infrastructure;

namespace TailBlazer.Views.Searching
{
    public class SearchOptionsViewModel : AbstractNotifyPropertyChanged, IDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();

        private readonly IDisposable _cleanUp;
        private int _selectedIndex;
        public SearchHints SearchHints { get; }
        public ISearchProxyCollection Local { get; }
        public ISearchProxyCollection Global { get; }

        public SearchOptionsViewModel(ISearchMetadataCollection metadataCollection,
            IGlobalSearchOptions globalSearchOptions,
            ISearchProxyCollectionFactory searchProxyCollectionFactory,
            ISearchMetadataFactory searchMetadataFactory,
            ISchedulerProvider schedulerProvider,
            SearchHints searchHints)
        {
            SearchHints = searchHints;

            var global = globalSearchOptions.Metadata;
            var local = metadataCollection;

            Action<SearchMetadata> changeScopeAction = meta =>
            {
                if (meta.IsGlobal)
                {
                    //make global
                    global.Remove(meta.SearchText);
                    var newValue = new SearchMetadata(meta, local.NextIndex(),false);
                    local.AddorUpdate(newValue);
                }
                else
                {
                    //make local
                    local.Remove(meta.SearchText);
                    var newValue = new SearchMetadata(meta, global.NextIndex(), true);
                    global.AddorUpdate(newValue);
                }
            };

            Local = searchProxyCollectionFactory.Create(local, Id, changeScopeAction);
            Global = searchProxyCollectionFactory.Create(global, Id, changeScopeAction);

            //command to add the current search to the tail collection
            var searchInvoker = SearchHints.SearchRequested
                .ObserveOn(schedulerProvider.Background)
                .Subscribe(request =>
                {
                    var isGlobal = SelectedIndex == 1;
                    var meta = searchMetadataFactory.Create(request.Text,
                        request.UseRegEx,
                        metadataCollection.NextIndex(),
                        false,
                        isGlobal);

                    if (SelectedIndex == 0)
                    {
                        local.AddorUpdate(meta);
                    }
                    else
                    {
                        global.AddorUpdate(meta);
                    }
                });

            _cleanUp = new CompositeDisposable(searchInvoker,
                searchInvoker,
                SearchHints,
                Global,
                Local);
        }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set { SetAndRaise(ref _selectedIndex, value); }
        }

        public void Dispose()
        {
            _cleanUp.Dispose();
        }
    }
}