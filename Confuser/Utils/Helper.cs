using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Mono.Cecil;
using System.Drawing;
using System.Windows.Controls;
using System.Globalization;
using Confuser.Core;

namespace Confuser
{
    class LeftMarginMultiplierConverter : IValueConverter
    {
        public double Length { get; set; }

        static int GetDepth(TreeViewItem item)
        {
            FrameworkElement elem = item;
            while (VisualTreeHelper.GetParent(elem) != null)
            {
                var tvi = VisualTreeHelper.GetParent(elem) as TreeViewItem;
                if (tvi != null)
                    return GetDepth(tvi) + 1;
                elem = VisualTreeHelper.GetParent(elem) as FrameworkElement;
            }
            return 0;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var item = value as TreeViewItem;
            if (item == null)
                return new Thickness(0);

            return new Thickness(Length * GetDepth(item), 0, 0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }
    class BoolToVisConverter : IValueConverter
    {
        public static BoolToVisConverter Instance = new BoolToVisConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? Visibility.Visible : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
    class TabSizeConverter : IValueConverter
    {
        public static TabSizeConverter Instance = new TabSizeConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            TabControl tabControl = value as TabControl;
            double maxWidth = 0;
            for (int i = 0; i < tabControl.Items.Count; i++)
            {
                var container = tabControl.ItemContainerGenerator.ContainerFromIndex(i);
                maxWidth = Math.Max(maxWidth, (container as UIElement).DesiredSize.Width);
            }
            return maxWidth + 10;
        }

        public object ConvertBack(object value, Type targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
    class PresetConverter : IValueConverter
    {
        public static PresetConverter Instance = new PresetConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((PrjPreset)value == PrjPreset.Undefined) return "Custom";
            return value.ToString();
        }

        public object ConvertBack(object value, Type targetTypes, object parameter, CultureInfo culture)
        {
            if ((string)value == "Custom") return PrjPreset.Undefined;
            return (PrjPreset)Enum.Parse(typeof(PrjPreset), (string)value);
        }
    }
    static class Helper
    {

        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, int dwFlags);
        [DllImport("kernel32.dll")]
        private static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, int lpType);
        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);
        [DllImport("kernel32.dll")]
        private static extern IntPtr LockResource(IntPtr hResData);
        [DllImport("kernel32.dll")]
        private static extern int SizeofResource(IntPtr hModule, IntPtr hResInfo);
        [DllImport("kernel32.dll")]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool EnumResourceNames(IntPtr hModule, int lpszType, EnumResNameProc lpEnumFunc, IntPtr lParam);
        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Auto)]
        private delegate bool EnumResNameProc(IntPtr hModule, int lpszType, IntPtr lpszName, IntPtr lParam);

        public static BitmapSource GetIcon(string path)
        {
            return GetIcon(path, false, false);

            //IntPtr hMod = LoadLibraryEx(path, IntPtr.Zero, 0x00000002);
            //MemoryStream mem = null;
            //EnumResourceNames(hMod, 3 + 11, new EnumResNameProc(delegate(IntPtr hModule, int lpszType, IntPtr lpszName, IntPtr lParam)
            //{
            //    if (lpszType == 3 + 11)
            //    {
            //        IntPtr res = FindResource(hMod, lpszName, 3 + 11);
            //        IntPtr dat = LoadResource(hMod, res);
            //        IntPtr ptr = LockResource(dat);
            //        int size = SizeofResource(hMod, res);
            //        Console.WriteLine(ptr.ToString("X8"));
            //        Console.WriteLine(size.ToString("X8"));
            //        Console.WriteLine();
            //        byte[] byteArr = new byte[size];
            //        Marshal.Copy(ptr, byteArr, 0, size);

            //        mem = new MemoryStream();
            //        BinaryWriter wtr = new BinaryWriter(mem);
            //        int count = BitConverter.ToUInt16(byteArr, 4);
            //        int offset = 6 + (0x10 * count);
            //        wtr.Write(byteArr, 0, 6);
            //        for (int i = 0; i < count; i++)
            //        {
            //            wtr.BaseStream.Seek(6 + (0x10 * i), SeekOrigin.Begin);
            //            wtr.Write(byteArr, 6 + (14 * i), 12);
            //            wtr.Write(offset);
            //            IntPtr id = (IntPtr)BitConverter.ToUInt16(byteArr, (6 + (14 * i)) + 12);

            //            IntPtr icoRes = FindResource(hMod, id, 3);
            //            IntPtr icoDat = LoadResource(hMod, icoRes);
            //            IntPtr icoPtr = LockResource(icoDat);
            //            int icoSize = SizeofResource(hMod, icoRes);
            //            byte[] img = new byte[icoSize];
            //            Marshal.Copy(icoPtr, img, 0, icoSize);

            //            wtr.BaseStream.Seek(offset, SeekOrigin.Begin);
            //            wtr.Write(img, 0, img.Length);
            //            offset += img.Length;
            //        }
            //        return false;
            //    }
            //    return true;
            //}), IntPtr.Zero);
            //FreeLibrary(hMod);
            //if (mem == null) return null;
            //IconBitmapDecoder decoder = new IconBitmapDecoder(mem, 0, 0);
            //BitmapSource ret = decoder.Frames[0];
            //double curr = 0;
            //foreach (BitmapSource src in decoder.Frames)
            //{
            //    if (src.Width > curr && src.Width <= 64)
            //    {
            //        ret = src;
            //        curr = src.Width;
            //        if (curr == 64) break;
            //    }
            //}
            //return ret;
        }
        static BitmapSource GetIcon(string path, bool smallIcon, bool isDirectory)
        {
            // SHGFI_USEFILEATTRIBUTES takes the file name and attributes into account if it doesn't exist
            uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES;
            if (smallIcon)
                flags |= SHGFI_SMALLICON;

            uint attributes = FILE_ATTRIBUTE_NORMAL;
            if (isDirectory)
                attributes |= FILE_ATTRIBUTE_DIRECTORY;

            SHFILEINFO shfi;
            if (0 != SHGetFileInfo(
                        path,
                        attributes,
                        out shfi,
                        (uint)Marshal.SizeOf(typeof(SHFILEINFO)),
                        flags))
            {
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                            shfi.hIcon,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
            }
            return null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32")]
        private static extern int SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint flags);

        private const uint FILE_ATTRIBUTE_READONLY = 0x00000001;
        private const uint FILE_ATTRIBUTE_HIDDEN = 0x00000002;
        private const uint FILE_ATTRIBUTE_SYSTEM = 0x00000004;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        private const uint FILE_ATTRIBUTE_ARCHIVE = 0x00000020;
        private const uint FILE_ATTRIBUTE_DEVICE = 0x00000040;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        private const uint FILE_ATTRIBUTE_TEMPORARY = 0x00000100;
        private const uint FILE_ATTRIBUTE_SPARSE_FILE = 0x00000200;
        private const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400;
        private const uint FILE_ATTRIBUTE_COMPRESSED = 0x00000800;
        private const uint FILE_ATTRIBUTE_OFFLINE = 0x00001000;
        private const uint FILE_ATTRIBUTE_NOT_CONTENT_INDEXED = 0x00002000;
        private const uint FILE_ATTRIBUTE_ENCRYPTED = 0x00004000;
        private const uint FILE_ATTRIBUTE_VIRTUAL = 0x00010000;

        private const uint SHGFI_ICON = 0x000000100;     // get icon
        private const uint SHGFI_DISPLAYNAME = 0x000000200;     // get display name
        private const uint SHGFI_TYPENAME = 0x000000400;     // get type name
        private const uint SHGFI_ATTRIBUTES = 0x000000800;     // get attributes
        private const uint SHGFI_ICONLOCATION = 0x000001000;     // get icon location
        private const uint SHGFI_EXETYPE = 0x000002000;     // return exe type
        private const uint SHGFI_SYSICONINDEX = 0x000004000;     // get system icon index
        private const uint SHGFI_LINKOVERLAY = 0x000008000;     // put a link overlay on icon
        private const uint SHGFI_SELECTED = 0x000010000;     // show icon in selected state
        private const uint SHGFI_ATTR_SPECIFIED = 0x000020000;     // get only specified attributes
        private const uint SHGFI_LARGEICON = 0x000000000;     // get large icon
        private const uint SHGFI_SMALLICON = 0x000000001;     // get small icon
        private const uint SHGFI_OPENICON = 0x000000002;     // get open icon
        private const uint SHGFI_SHELLICONSIZE = 0x000000004;     // get shell size icon
        private const uint SHGFI_PIDL = 0x000000008;     // pszPath is a pidl
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;     // use passed dwFileAttribute

        public static T FindChild<T>(DependencyObject parent, string childName)
            where T : DependencyObject
        {
            // Confirm parent and childName are valid. 
            if (parent == null) return null;

            T foundChild = null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                // If the child is not of the request child type child
                T childType = child as T;
                if (childType == null)
                {
                    // recursively drill down the tree
                    foundChild = FindChild<T>(child, childName);

                    // If the child is found, break so we do not overwrite the found child. 
                    if (foundChild != null) break;
                }
                else if (!string.IsNullOrEmpty(childName))
                {
                    var frameworkElement = child as FrameworkElement;
                    // If the child's name is set for search
                    if (frameworkElement != null && frameworkElement.Name == childName)
                    {
                        // if the child's name is of the request name
                        foundChild = (T)child;
                        break;
                    }
                    foundChild = FindChild<T>(child, childName);
                    if (foundChild != null) break;
                }
                else
                {
                    // child element found.
                    foundChild = (T)child;
                    break;
                }
            }

            return foundChild;
        }

        public static T FindParent<T>(DependencyObject child)
            where T : DependencyObject
        {
            DependencyObject d = child;
            while (d != null)
            {
                d = VisualTreeHelper.GetParent(d);
                if (d is T) return (T)d;
            }
            return null;
        }
    }

    class AssemblyInfo
    {
        public AssemblyDefinition Assembly { get; set; }
        public string ShortName { get { return Path.GetFileName(Assembly.MainModule.FullyQualifiedName); } }
    }
}
