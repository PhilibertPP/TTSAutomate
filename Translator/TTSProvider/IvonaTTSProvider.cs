﻿using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace TTSAutomate
{
    partial class IvonaTTSProvider : TTSProvider
    {
        bool disabled = false;
        public IvonaTTSProvider()
        {
            Name = "Ivona Text To Speech";
            ProviderClass = Class.Web;
            HasVoices = true;
            HasDiscreteSpeed = true;
            HasDiscreteVolume = true;
            BackgroundWorker loadVoicesWorker = new BackgroundWorker();
            
            loadVoicesWorker.DoWork += delegate
            {
                try
                {
                    AvailableVoices = IvonaListVoices().Voices;
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
                }
                catch (Exception Ex)
                {
                    AvailableVoices.Add(new Voice { Name = "Ivona Voices are Disabled", Language = "en-US", Gender = "None"});
                    SelectedVoice = AvailableVoices[0];
                    disabled = true;
                }
            };
            loadVoicesWorker.RunWorkerAsync();

            AvailableSpeeds.AddRange(new String[] { "x-slow", "slow", "medium", "fast", "x-fast" });
            SelectedDiscreteSpeed = "medium";

            AvailableVolumes.AddRange(new String[] { "silent", "x-soft", "soft", "medium", "loud", "x-loud" });
            SelectedDiscreteVolume = "medium";
        }

        public override void DownloadItem(PhraseItem item, string folder)
        {
            try
            {
                new Task(() =>
                {
                    File.WriteAllBytes(String.Format("{0}\\mp3\\{1}\\{2}.mp3", folder, item.Folder, item.FileName), IvonaCreateSpeech(item.Phrase, SelectedVoice));
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
                    File.WriteAllBytes(String.Format("{0}\\mp3\\{1}\\{2}.mp3", folder, item.Folder, item.FileName), IvonaCreateSpeech(item.Phrase, SelectedVoice));
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
            if (!disabled)
            {
                byte[] mp3 = IvonaCreateSpeech(item.Phrase, SelectedVoice);
                //File.WriteAllBytes(String.Format("{0}\\mp3\\{1}\\{2}.mp3", Path.GetTempPath(), item.Folder, item.FileName), );
                MainWindow.PlayAudioStream(mp3);
            }
        }

        public byte[] IvonaCreateSpeech(string text, Voice selectedVoice)
        {
            var date = DateTime.UtcNow;

            String SSMLText = String.Format(@"
<?xml version=""1.0""?>
<speak xmlns=""http://www.w3.org/2001/10/synthesis"" version=""1.0"">
<p>
<s>{0}</s>
</p>
</speak>
",text.Replace("&","&amp;"));
            byte[] stringbytes = Encoding.UTF8.GetBytes(SSMLText);
            SSMLText = Encoding.UTF8.GetString(stringbytes);

            string algorithm = "AWS4-HMAC-SHA256";
            string regionName = Properties.Settings.Default.IvonaRegion;
            string serviceName = "tts";
            string method = "POST";
            string canonicalUri = "/CreateSpeech";
            string canonicalQueryString = "";
            string contentType = "application/json";


            string host = serviceName + "." + regionName + ".ivonacloud.com";

            var obj = new
            {
                Input = new
                {
                    Data = SSMLText,// Encoding.UTF8.GetString(Encoding.Default.GetBytes(text)),
                    Type = "application/ssml+xml"
                },
                OutputFormat = new
                {
                    Codec = "MP3",
                    SampleRate = 22050
                },
                Parameters = new
                {
                    Rate = SelectedDiscreteSpeed,
                    Volume = SelectedDiscreteVolume,
                    SentenceBreak = 500,
                    ParagraphBreak = 800
                },
                Voice = new
                {
                    Name = selectedVoice.Name,
                    Language = selectedVoice.Language,
                    Gender = selectedVoice.Gender
                }
            };
            var requestPayload = new JavaScriptSerializer().Serialize(obj);

            var hashedRequestPayload = HexEncode(Hash(ToBytes(requestPayload)));

            var dateStamp = date.ToString("yyyyMMdd");
            var requestDate = date.ToString("yyyyMMddTHHmmss") + "Z";
            var credentialScope = string.Format("{0}/{1}/{2}/aws4_request", dateStamp, regionName, serviceName);

            var headers = new SortedDictionary<string, string>
            {
                {"content-type", contentType},
                {"host", host},
                {"x-amz-date", requestDate}
            };

            string canonicalHeaders =
                string.Join("\n", headers.Select(x => x.Key.ToLowerInvariant() + ":" + x.Value.Trim())) + "\n";
            const string signedHeaders = "content-type;host;x-amz-date";

            // Task 1: Create a Canonical Request For Signature Version 4

            var canonicalRequest = method + '\n' + canonicalUri + '\n' + canonicalQueryString +
                                   '\n' + canonicalHeaders + '\n' + signedHeaders + '\n' + hashedRequestPayload;

            var hashedCanonicalRequest = HexEncode(Hash(ToBytes(canonicalRequest)));

            // Task 2: Create a String to Sign for Signature Version 4
            // StringToSign  = Algorithm + '\n' + RequestDate + '\n' + CredentialScope + '\n' + HashedCanonicalRequest

            var stringToSign = string.Format("{0}\n{1}\n{2}\n{3}", algorithm, requestDate, credentialScope,
                hashedCanonicalRequest);

            // Task 3: Calculate the AWS Signature Version 4

            // HMAC(HMAC(HMAC(HMAC("AWS4" + kSecret,"20130913"),"eu-west-1"),"tts"),"aws4_request")
            byte[] signingKey = GetSignatureKey(SecretKey, dateStamp, regionName, serviceName);

            // signature = HexEncode(HMAC(derived-signing-key, string-to-sign))
            var signature = HexEncode(HmacSha256(stringToSign, signingKey));

            // Task 4: Prepare a signed request
            // Authorization: algorithm Credential=access key ID/credential scope, SignedHeadaers=SignedHeaders, Signature=signature

            var authorization =
                string.Format("{0} Credential={1}/{2}/{3}/{4}/aws4_request, SignedHeaders={5}, Signature={6}",
                    algorithm, AccessKey, dateStamp, regionName, serviceName, signedHeaders, signature);

            // Send the request

            var webRequest = WebRequest.Create("https://" + host + canonicalUri);

            var bytes = ToBytes(requestPayload);
            webRequest.Method = method;
            webRequest.Timeout = 20000;
            webRequest.ContentType = contentType;
            webRequest.Headers.Add("X-Amz-date", requestDate);
            webRequest.Headers.Add("Authorization", authorization);
            webRequest.Headers.Add("x-amz-content-sha256", hashedRequestPayload);
            webRequest.ContentLength = bytes.Length;

            try
            {
                using (Stream newStream = webRequest.GetRequestStream())
                {
                    newStream.Write(bytes, 0, bytes.Length);
                    newStream.Flush();
                }
                var response = (HttpWebResponse)webRequest.GetResponse();

                using (Stream responseStream = response.GetResponseStream())
                {
                    if (responseStream != null)
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            responseStream.CopyTo(memoryStream);
                            return memoryStream.ToArray();
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Failed : {0}", ex);

            }
            return new byte[0];
        }

        public SupportedVoices IvonaListVoices()
        {
            var date = DateTime.UtcNow;

            string algorithm = "AWS4-HMAC-SHA256";
            string regionName = Properties.Settings.Default.IvonaRegion;
            string serviceName = "tts";
            string method = "POST";
            string canonicalUri = "/ListVoices";
            string canonicalQueryString = "";
            string contentType = "application/json";

            string host = serviceName + "." + regionName + ".ivonacloud.com";

            var obj = new
            {
                Voice = new
                {
                }
            };
            var requestPayload = new JavaScriptSerializer().Serialize(obj);

            var hashedRequestPayload = HexEncode(Hash(ToBytes(requestPayload)));

            var dateStamp = date.ToString("yyyyMMdd");
            var requestDate = date.ToString("yyyyMMddTHHmmss") + "Z";
            var credentialScope = string.Format("{0}/{1}/{2}/aws4_request", dateStamp, regionName, serviceName);

            var headers = new SortedDictionary<string, string>
            {
                {"content-type", contentType},
                {"host", host},
                {"x-amz-date", requestDate}
            };

            string canonicalHeaders =
                string.Join("\n", headers.Select(x => x.Key.ToLowerInvariant() + ":" + x.Value.Trim())) + "\n";
            const string signedHeaders = "content-type;host;x-amz-date";

            // Task 1: Create a Canonical Request For Signature Version 4

            var canonicalRequest = method + '\n' + canonicalUri + '\n' + canonicalQueryString +
                                   '\n' + canonicalHeaders + '\n' + signedHeaders + '\n' + hashedRequestPayload;

            var hashedCanonicalRequest = HexEncode(Hash(ToBytes(canonicalRequest)));

            // Task 2: Create a String to Sign for Signature Version 4
            // StringToSign  = Algorithm + '\n' + RequestDate + '\n' + CredentialScope + '\n' + HashedCanonicalRequest

            var stringToSign = string.Format("{0}\n{1}\n{2}\n{3}", algorithm, requestDate, credentialScope,
                hashedCanonicalRequest);

            // Task 3: Calculate the AWS Signature Version 4

            // HMAC(HMAC(HMAC(HMAC("AWS4" + kSecret,"20130913"),"eu-west-1"),"tts"),"aws4_request")
            byte[] signingKey = GetSignatureKey(SecretKey, dateStamp, regionName, serviceName);

            // signature = HexEncode(HMAC(derived-signing-key, string-to-sign))
            var signature = HexEncode(HmacSha256(stringToSign, signingKey));

            // Task 4: Prepare a signed request
            // Authorization: algorithm Credential=access key ID/credential scope, SignedHeadaers=SignedHeaders, Signature=signature

            var authorization =
                string.Format("{0} Credential={1}/{2}/{3}/{4}/aws4_request, SignedHeaders={5}, Signature={6}",
                    algorithm, AccessKey, dateStamp, regionName, serviceName, signedHeaders, signature);

            // Send the request

            var webRequest = WebRequest.Create("https://" + host + canonicalUri);

            webRequest.Method = method;
            webRequest.Timeout = 20000;
            webRequest.ContentType = contentType;
            webRequest.Headers.Add("X-Amz-date", requestDate);
            webRequest.Headers.Add("Authorization", authorization);
            webRequest.Headers.Add("x-amz-content-sha256", hashedRequestPayload);
            webRequest.ContentLength = requestPayload.Length;

            using (Stream newStream = webRequest.GetRequestStream())
            {
                newStream.Write(ToBytes(requestPayload), 0, requestPayload.Length);
                newStream.Flush();
            }

            var response = (HttpWebResponse)webRequest.GetResponse();

            using (Stream responseStream = response.GetResponseStream())
            {
                if (responseStream != null)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        //responseStream.CopyTo(memoryStream);
                        String responseString = new StreamReader(responseStream).ReadToEnd();

                        return new JavaScriptSerializer().Deserialize<SupportedVoices>(responseString);
                    }
                }
            }

            return new SupportedVoices();// new List<Voice>());
        }

        private static byte[] GetSignatureKey(String key, String dateStamp, String regionName, String serviceName)
        {
            byte[] kDate = HmacSha256(dateStamp, ToBytes("AWS4" + key));
            byte[] kRegion = HmacSha256(regionName, kDate);
            byte[] kService = HmacSha256(serviceName, kRegion);
            return HmacSha256("aws4_request", kService);
        }

        private static byte[] ToBytes(string str)
        {
            return Encoding.UTF8.GetBytes(str.ToCharArray());
        }

        private static string HexEncode(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static byte[] Hash(byte[] bytes)
        {
            return SHA256.Create().ComputeHash(bytes);
        }

        private static byte[] HmacSha256(String data, byte[] key)
        {
            return new HMACSHA256(key).ComputeHash(ToBytes(data));
        }
    }
    public class SupportedVoices
    {
        public List<Voice> Voices { get; set; }
    }


}
