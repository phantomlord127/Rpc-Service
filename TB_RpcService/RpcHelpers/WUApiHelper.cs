using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WUApiLib;

namespace TB_RpcService.RpcHelpers
{
    public class WUApiHelper
    {
        private UpdateSession _updateSession = new UpdateSession();
        private IUpdateSearcher _updateSearcher;
        private IUpdateDownloader _updateDownloader;
        private IUpdateInstaller _updateInstaller;
        private UpdateCollection _updateCollection = new UpdateCollection();
        private UpdateCollection _updateInstallCollection = new UpdateCollection();

        public void StartWU()
        {
            _updateSearcher = _updateSession.CreateUpdateSearcher();
            _updateSearcher.Online = false;
            SearchCompletedCallback searchCompletedCallback = new SearchCompletedCallback(this);
            _updateSearcher.BeginSearch("IsInstalled=1 And IsHidden=0", searchCompletedCallback, null);
        }

        public void SearchCompletedCallback(ISearchJob searchJob)
        {
            ISearchResult searchResult = _updateSearcher.EndSearch(searchJob);
            if (searchResult.Updates.Count > 0)
            {
                foreach (IUpdate u in searchResult.Updates)
                {
                    u.AcceptEula();
                    _updateCollection.Add(u);
                }
                _updateDownloader = _updateSession.CreateUpdateDownloader();
                _updateDownloader.Updates = searchResult.Updates;
                _updateDownloader.BeginDownload(new DownloadProgressChangedCallback(this), new DownloadCompletedCallback(this), null);
            }
            else
            {
                //no Updates found
            }
        }

        public void DownloadCompletedCallback(IDownloadJob downloadJob)
        {
            IDownloadResult downloadResult = _updateDownloader.EndDownload(downloadJob);
            UpdateCollection updateErrorCollection = new UpdateCollection();
            foreach (IUpdate u in _updateCollection)
            {
                if (u.IsDownloaded)
                {
                    _updateInstallCollection.Add(u);
                }
                else
                {
                    updateErrorCollection.Add(u);
                }
            }
            if (updateErrorCollection.Count > 0)
            {
                //return message
            }
            if (_updateInstallCollection.Count > 0)
            {
                _updateInstaller = _updateSession.CreateUpdateInstaller();
                _updateInstaller.BeginInstall(new InstallProgressChangedCallback(this), new InstallCompletedCallback(this), null);
            }
        }

        public void InstallCompletedCallback(IInstallationJob installJob)
        {
            IInstallationResult installResult = _updateInstaller.EndInstall(installJob);
            bool installSucess = false;
            if (installResult.RebootRequired)
            {
                //reboot
            }
            foreach (IUpdate u in _updateInstallCollection)
            {
                if (!u.IsInstalled)
                {
                    installSucess = false;
                }
            }
            if (!installSucess)
            {
                //Error in installation
            }
            else
            {
                //Installation completed
            }
        }
    }

    class SearchCompletedCallback : ISearchCompletedCallback
    {
        public WUApiHelper WUApiHelper { get; set; }

        public SearchCompletedCallback(WUApiHelper helper)
        {
            WUApiHelper = helper;
        }

        public void Invoke(ISearchJob searchJob, ISearchCompletedCallbackArgs args)
        {
            WUApiHelper.SearchCompletedCallback(searchJob);
        }
    }

    class DownloadProgressChangedCallback : IDownloadProgressChangedCallback
    {
        public WUApiHelper WUApiHelper { get; set; }

        public DownloadProgressChangedCallback(WUApiHelper helper)
        {
            WUApiHelper = helper;
        }

        public void Invoke(IDownloadJob downLoadJob, IDownloadProgressChangedCallbackArgs args)
        {
            IDownloadProgress progress = args.Progress;
            //progress.PercentComplete;
        }
    }

    class DownloadCompletedCallback : IDownloadCompletedCallback
    {
        public WUApiHelper WUApiHelper { get; set; }

        public DownloadCompletedCallback(WUApiHelper helper)
        {
            WUApiHelper = helper;
        }

        public void Invoke(IDownloadJob downLoadJob, IDownloadCompletedCallbackArgs args)
        {
            WUApiHelper.DownloadCompletedCallback(downLoadJob);
        }
    }

    class InstallProgressChangedCallback : IInstallationProgressChangedCallback
    {
        public WUApiHelper WUApiHelper { get; set; }

        public InstallProgressChangedCallback(WUApiHelper helper)
        {
            WUApiHelper = helper;
        }

        public void Invoke(IInstallationJob installJob, IInstallationProgressChangedCallbackArgs args)
        {
            IInstallationProgress progress = args.Progress;
        }
    }

    class InstallCompletedCallback : IInstallationCompletedCallback
    {
        public WUApiHelper WUApiHelper { get; set; }

        public InstallCompletedCallback(WUApiHelper helper)
        {
            WUApiHelper = helper;
        }

        public void Invoke(IInstallationJob installJob, IInstallationCompletedCallbackArgs args)
        {
            WUApiHelper.InstallCompletedCallback(installJob);
        }
    }
}
