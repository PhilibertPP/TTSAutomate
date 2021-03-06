﻿using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace TTSAutomate
{
    class GoogleTTSProvider : TTSProvider
    {

        public GoogleTTSProvider()
        {
            Name = "Google Text To Speech";
            ProviderClass = Class.Web;
            HasVoices = true;
            BackgroundWorker loadVoicesWorker = new BackgroundWorker();
            loadVoicesWorker.DoWork += delegate
            {
                List<CultureInfo> cultures = new List<CultureInfo>();
                cultures.AddRange(CultureInfo.GetCultures(CultureTypes.SpecificCultures));
                cultures.Sort((x, y) => x.DisplayName.CompareTo(y.DisplayName));
                AvailableVoices = cultures.Select(x => new Voice() { Name = x.DisplayName, Language = x.Name }).ToList();
                if (this.Name == Properties.Settings.Default.LastTTSProvider)
                {
                    if (Properties.Settings.Default.RememberLanguageSettings)
                    {
                        SelectedVoice = AvailableVoices.Find(n => n.Name == Properties.Settings.Default.LastTTSVoice);
                    }
                    else
                    {
                        SelectedVoice = AvailableVoices[0];
                    }
                }

            };
            loadVoicesWorker.RunWorkerAsync();
        }

        public override void DownloadItem(PhraseItem item, string folder)
        {
            try
            {
                new Task(() =>
                {
                    using (WebClient wc = new WebClient())
                    {
                        wc.DownloadFile(String.Format("http://translate.google.com/translate_tts?ie=UTF-8&total=1&idx=0&client=tw-ob&q={0}&tl={1}", item.Phrase.Replace("&", "%26"), SelectedVoice.Language), String.Format("{0}\\mp3\\{1}\\{2}.mp3", folder, item.Folder, item.FileName));
                    }
                    ConvertToWav(item, folder, false, new String[] { Name, SelectedVoice.Name, SelectedDiscreteSpeed, SelectedDiscreteVolume });
                }).Start();

            }
            catch (Exception Ex)
            {
                Logger.Log(Ex.ToString());
                item.DownloadComplete = false;
            }
        }

        public override void DownloadAndPlayItem(PhraseItem item, string folder)
        {
            try
            {
                new Task(() =>
                {
                    using (WebClient wc = new WebClient())
                    {
                        wc.DownloadFile(String.Format("http://translate.google.com/translate_tts?ie=UTF-8&total=1&idx=0&client=tw-ob&q={0}&tl={1}", item.Phrase.Replace("&", "%26"), SelectedVoice.Language), String.Format("{0}\\mp3\\{1}\\{2}.mp3", folder, item.Folder, item.FileName));
                    }
                    ConvertToWav(item, folder, true, new String[] { Name, SelectedVoice.Name, SelectedDiscreteSpeed, SelectedDiscreteVolume });
                }).Start();
            }
            catch (Exception Ex)
            {
                Logger.Log(Ex.ToString());
                item.DownloadComplete = false;
            }
        }

        public override void Play(PhraseItem item)
        {
            using (WebClient wc = new WebClient())
            {
                MainWindow.PlayAudioStream(wc.DownloadData(String.Format("http://translate.google.com/translate_tts?ie=UTF-8&total=1&idx=0&client=tw-ob&q={0}&tl={1}", item.Phrase.Replace("&", "%26"), SelectedVoice.Language)));
            }
        }
    }

}
