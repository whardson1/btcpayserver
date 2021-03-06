﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace BTCPayServer.Views
{
    public static class ViewsRazor
    {
        public const string ACTIVE_PAGE_KEY = "ActivePage";
        public static void SetActivePageAndTitle<T>(this ViewDataDictionary viewData, T activePage, string title = null)
            where T : IConvertible
        {
            viewData["Title"] = title ?? activePage.ToString();
            viewData[ACTIVE_PAGE_KEY] = activePage;
        }

        public static string IsActivePage<T>(this ViewDataDictionary viewData, T page)
            where T : IConvertible
        {
            var activePage = (T)viewData[ACTIVE_PAGE_KEY];
            return page.Equals(activePage) ? "active" : null;
        }
    }
}
