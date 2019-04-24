﻿using JetBrains.Annotations;

namespace Abp.Web.Mvc.Alerts
{
    public class AlertMessage
    {
        [NotNull]
        public string Text
        {
            get => _text;
            set => _text = Check.NotNullOrWhiteSpace(value, nameof(value));
        }
        private string _text;

        public AlertType Type { get; set; }

        [CanBeNull]
        public string Title { get; set; }

        public bool Dismissible { get; set; }

        public AlertDisplayType DisplayType { get; set; }

        public AlertMessage(AlertType type, [NotNull] string text, string title = null, bool dismissible = true, AlertDisplayType displayType = AlertDisplayType.PageAlert)
        {
            Type = type;
            Text = Check.NotNullOrWhiteSpace(text, nameof(text));
            Title = title;
            Dismissible = dismissible;
            DisplayType = displayType;
        }
    }
}