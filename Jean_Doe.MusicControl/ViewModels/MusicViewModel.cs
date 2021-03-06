﻿using System.ComponentModel;
using Jean_Doe.Common;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
namespace Jean_Doe.MusicControl
{
    public class MusicViewModel : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected void Notify(string property)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(property));
        }
        #endregion
        protected IMusic music;
        private BitmapImage imageSrc;
        public BitmapImage ImageSource
        {
            get { return imageSrc; }
            set
            {
                imageSrc = value; Notify("ImageSource");
                //LogoColor = ImageHelper.GetAverageColor(value);
            }
        }
        private bool isNowPlaying;

        public bool IsNowPlaying
        {
            get { return isNowPlaying; }
            set { isNowPlaying = value; Notify("IsNowPlaying"); }
        }
        public bool InFav
        {
            get { return false; }
        }
        protected string typeImage;
        public string TypeImage { get { return typeImage; } set { typeImage = value; Notify("TypeImage"); } }
        protected string logoColor = ImageHelper.DefaultColor;
        public string LogoColor { get { return logoColor; } set { logoColor = value; Notify("LogoColor"); } }
        public MusicViewModel(IMusic m)
        {
            music = m;
            if (m.Logo != null)
                m.Logo = m.Logo.Replace("_1.jpg", ".jpg");
        }
        protected void InitLogo(string imgId)
        {
            ImageManager.Get(imgId, Logo, ApplyLogo);
        }
        public void ApplyLogo(BitmapImage src)
        {
            UIHelper.RunOnUI(() => ImageSource = src);
        }
        private bool canAnimate = true;

        public bool CanAnimate
        {
            get { return canAnimate; }
            set { canAnimate = value; }
        }
        private string searchStr = null;
        public double Recommends
        {
            get
            {
                double res = 0;
                double.TryParse(MusicHelper.Get(music.JsonObject, "recommends", "count_likes"), out res);
                return res;
            }
        }
        public string SearchStr
        {
            get
            {
                if (searchStr == null)
                {
                    var text = new List<string>();
                    text.Add(Name.ToLower());
                    if (this is IHasAlbum)
                        text.Add(((IHasAlbum)this).AlbumName.ToLower());
                    if (this is IHasArtist)
                        text.Add(((IHasArtist)this).ArtistName.ToLower());
                    searchStr = string.Join(" ", text);
                }
                return searchStr;
            }
        }

        public bool HasDetail { get { return !string.IsNullOrEmpty(Description); } }
        public bool IsDetailShown { get; set; }
        public virtual string Name { get { return music.Name; } }
        public virtual string Description
        {
            get
            {
                return music.Get("reason", "description");
            }
            set
            {
            }
        }
        public virtual string Id { get { return music.Id; } }
        public virtual string Logo { get { return music.Logo; } }
        public virtual EnumMusicType Type { get { return music.Type; } }
    }
}
