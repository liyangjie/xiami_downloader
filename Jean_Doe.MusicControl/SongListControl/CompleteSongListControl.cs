﻿using Artwork.MessageBus;
using Artwork.MessageBus.Interfaces;
using Jean_Doe.Common;
using Jean_Doe.Downloader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
namespace Jean_Doe.MusicControl
{
    public class CompleteSongListControl : SongListControl,
        IHandle<MsgDownloadStateChanged>
    {
        public CompleteSongListControl()
        {
            Items.CollectionChanged += Items_CollectionChanged;
            MessageBus.Instance.Subscribe(this);

            combo_sort.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "最近下载", Tag = "Date_Dsc"});
            var l = new List<CharmAction>{
                new CharmAction("存为播放列表","\xE14C",this.btn_save_playlist_Click,isMultiSelect),
                new CharmAction("复制文件到剪贴板","\xE16F",this.btn_copy_Click,(s)=>true),
                new CharmAction("打开文件所在位置","\xE1A5",this.btn_open_click,IsOnlyType<IHasMusicPart>),
                new CharmAction("删除","\xE106",this.btn_remove_complete_Click,(s)=>true),
                new CharmAction("导入","\xE150",this.btn_import_click,s=>false)
            };
            foreach (var item in l)
            {
                actions[item.Label] = item;
            }
            addCommonActions();
        }



        protected override void ApplyFilter()
        {
            base.ApplyFilter();
            PlayList.NeedsRefresh();
        }

        


        void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Add) return;
            foreach (var item in e.NewItems.OfType<SongViewModel>())
            {
                var file = item.Song.FilePath;
                if (File.Exists(file))
                {
                    var date = new FileInfo(file).LastWriteTime;
                    UIHelper.RunOnUI(() =>
                        item.Date = date
                    );
                }
                if (item.Song.FilePath == null)
                {
                    item.Song.FilePath = Path.Combine(Global.AppSettings["DownloadFolder"], item.Dir, item.FileNameBase + ".mp3");
                }
            }
        }
        public void Handle(MsgDownloadStateChanged message)
        {
            var item = message.Item as SongViewModel;
            if (item == null || !item.HasMp3)
                return;
            item.Song.DownloadState = "complete";
            SongViewModel.RequestSave(item);
            Items.AddItems(new List<IMusic> { item.Song }, true);
        }


        protected override void item_double_click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            btn_play_Click(sender, e);
            ActionBarService.Refresh();
        }
        Regex reg = new Regex("^(\\d+)$");
        Regex reg_ids = new Regex("^(\\d+) (\\d+) (\\d+)$");
        void btn_import_click(object sender, RoutedEventArgs e)
        {
            var dir = Global.AppSettings["DownloadFolder"];
            Task.Run(async () =>
            {
                var buffer = new List<IMusic>();
                int bufferLength = 10;
                var mp3s = Directory.EnumerateFiles(dir, "*.mp3").ToArray();
                foreach (var item in mp3s)
                {
                    try
                    {
                        var mp3 = TagLib.File.Create(item);
                        var tags = mp3.Tag;
                        if (tags.Comment == null) continue;
                        var id = "";
                        var artistid = "";
                        var albumid = "";
                        var logo = "";
                        var m = reg_ids.Match(tags.Comment);
                        if (!m.Success)
                        {
                            m = reg.Match(tags.Comment);
                            if (!m.Success) continue;
                            id = m.Groups[1].Value;
                            var obj = await NetAccess.Json(XiamiUrl.url_song, "id", id);
                            artistid = MusicHelper.Get(obj["song"], "artist_id");
                            albumid = MusicHelper.Get(obj["song"], "album_id");
                            logo = StringHelper.EscapeUrl(MusicHelper.Get(obj["song"], "logo"));
                            tags.Comment = string.Join(" ", new[] { id, artistid, albumid });
                            mp3.Save();
                        }
                        else
                        {
                            id = m.Groups[1].Value;
                            artistid = m.Groups[2].Value;
                            albumid = m.Groups[3].Value;
                        }
                        var art = System.IO.Path.Combine(Global.BasePath, "cache", albumid + ".art");
                        if (!File.Exists(art))
                        {
                            if (string.IsNullOrEmpty(logo))
                            {
                                var obj = await NetAccess.Json(XiamiUrl.url_song, "id", id);
                                logo = StringHelper.EscapeUrl(MusicHelper.Get(obj["song"], "logo"));
                            }
                            await new System.Net.WebClient().DownloadFileTaskAsync(logo, art);
                        }
                        var song = new Song
                        {
                            Id = id,
                            ArtistId = artistid,
                            AlbumId = albumid,
                            Name = tags.Title,
                            ArtistName = tags.FirstPerformer,
                            AlbumName = tags.Album,
                            FilePath = item,
                            Logo = logo,
                        };
                        buffer.Add(song);
                    }
                    catch
                    {
                    }
                    if (buffer.Count == bufferLength)
                    {
                        var songs = new List<IMusic>();
                        foreach (var s in buffer.OfType<Song>())
                        {
                            SongViewModel.Get(s).HasMp3 = true;
                        }
                        songs.AddRange(buffer);
                        Items.AddItems(songs);
                        buffer.Clear();
                    }
                }
                if (buffer.Count > 0)
                {
                    foreach (var s in buffer.OfType<Song>())
                    {
                        SongViewModel.Get(s).HasMp3 = true;
                    }
                    Items.AddItems(buffer);
                }
            });

        }
        void btn_save_playlist_Click(object sender, RoutedEventArgs e)
        {
            if (!SelectedSongs.Any())
                return;
            var win = new System.Windows.Forms.SaveFileDialog
            {
                InitialDirectory = Global.AppSettings["DownloadFolder"],
                Filter = "播放列表文件 (*.m3u)|*.m3u",
                OverwritePrompt = true,
                Title = "存为播放列表"
            };
            if (win.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SavePlaylist(win.FileName);
            }
        }
        void btn_remove_complete_Click(object sender, RoutedEventArgs e)
        {
            var list = SelectedSongs.ToArray();
            Task.Run(() =>
            {
                SongViewModel.CanSave = false;
                foreach (var item in list)
                {
                    try
                    {
                        File.Delete(item.Song.FilePath);
                        File.Delete(Path.Combine(Global.BasePath, "cache", item.Id + ".mp3"));
                    }
                    catch
                    {
                    }
                    Remove(item);
                    SongViewModel.Remove(item.Id);
                }
                try
                {
                    PersistHelper.Delete(list.Select(x => x.Song).ToArray());
                }
                catch (Exception ex)
                {
                    Jean_Doe.Common.Logger.Error(ex);
                }
                SongViewModel.CanSave = true;
            });
        }
        void btn_copy_Click(object sender, RoutedEventArgs e)
        {
            var files = new System.Collections.Specialized.StringCollection();
            foreach (var item in SelectedSongs)
            {
                if (string.IsNullOrEmpty(item.Song.FilePath))
                    files.Add(System.IO.Path.Combine(".", item.Dir, item.FileNameBase + ".mp3"));
                else
                    files.Add(item.Song.FilePath);
            }
            if (files.Count > 0)
                Clipboard.SetFileDropList(files);
        }
        List<string> Playlist = new List<string>();
        string SavePlaylist(string path = null)
        {
            if (!SelectedSongs.Any())
                return null;
            Playlist.Clear();
            foreach (var item in SelectedSongs)
            {
                Playlist.Add(string.Format("#EXTINF:{0}", item.Id));
                if (string.IsNullOrEmpty(item.Song.FilePath))
                    Playlist.Add(System.IO.Path.Combine(".", item.Dir, item.FileNameBase + ".mp3"));
                else
                    Playlist.Add(item.Song.FilePath);
            }
            if (path == null)
                path = Global.AppSettings["DownloadFolder"] + "\\default.m3u";
            File.WriteAllLines(path, Playlist, Encoding.UTF8);
            return path;
        }

     

        protected override void btn_item_action_Click(object sender, RoutedEventArgs e)
        {
            base.btn_item_action_Click(sender, e);
        }
    }
}
