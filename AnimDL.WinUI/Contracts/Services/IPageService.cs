using System;

namespace AnimDL.WinUI.Contracts.Services;

public interface IPageService
{
    Type GetPageType(string key);
}
