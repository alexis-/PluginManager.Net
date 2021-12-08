#region License & Metadata

// The MIT License (MIT)
// 
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
// 
// 
// Modified On:  2020/03/18 16:40
// Modified By:  Alexis

#endregion




using System;
using System.ComponentModel;
using System.Threading;

namespace PluginManager
{
  /// <summary>
  ///   Global interceptor for Fody.PropertyChanged. Synchronizes notifications with the UI
  ///   thread. https://github.com/Fody/PropertyChanged/wiki/NotificationInterception
  /// </summary>
  [EditorBrowsable(EditorBrowsableState.Never)]
  public static class PropertyChangedNotificationInterceptor
  {
    #region Constants & Statics

    internal static SynchronizationContext GlobalSynchronizationContext { get; set; }

    #endregion




    #region Methods

    /// <summary>
    ///   Called for every OnPropertyChanged. For classes that already implement
    ///   OnPropertyChanged method then having a PropertyChangedNotificationInterceptor class will
    ///   have no effect on those classes.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="onPropertyChangedAction"></param>
    /// <param name="propertyName"></param>
    /// <param name="before"></param>
    /// <param name="after"></param>
    public static void Intercept(object target,
                                 Action onPropertyChangedAction,
                                 string propertyName,
                                 object before,
                                 object after)
    {
      if (GlobalSynchronizationContext != null)
        GlobalSynchronizationContext.Send(_ => onPropertyChangedAction(), null);

      else
        onPropertyChangedAction();
    }

    #endregion
  }
}
