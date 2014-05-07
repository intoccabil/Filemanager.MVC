// Filemanager ASP.NET MVC connector
// Author: David Hammond <dave@modernsignal.com>
// Based on ASHX connection by Ondřej "Yumi Yoshimido" Brožek | <cholera@hzspraha.cz>

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Configuration;
using System.Web.Helpers;
using System.Web.Mvc;
using System.Web.Script.Serialization;

// Do not forget to update namespace in FileManagerAreaRegistration.cs if controller is moved to another namespace.
namespace MyProject.Areas.FilemanagerArea.Controllers
{
    public class FilemanagerController : Controller
    {
        public FilemanagerController()
        {
            try
            {
                dynamic configuration = _json.DeserializeObject(
                    System.IO.File.ReadAllText(System.Web.HttpContext.Current.Server.MapPath(_configPath)));

                _rootPath = configuration["options"]["fileRoot"];
                _iconDirectory = Path.Combine(_filemanagerPath, configuration["icons"]["path"]);

                _allowedExtensions =
                    new DynamicJsonArray(configuration["security"]["uploadRestrictions"]).Select(
                        ext => ext.ToString().Insert(0, ".")).ToList();

                _imgExtensions = new DynamicJsonArray(configuration["images"]["imagesExt"]).Select(
                    ext => ext.ToString().Insert(0, ".")).ToList();
            }
            catch (Exception)
            {
                // Could not read configuration, set default values.
                _rootPath = WebConfigurationManager.AppSettings["Filemanager_RootPath"];
                _iconDirectory = WebConfigurationManager.AppSettings["Filemanager_IconDirectory"];
                _allowedExtensions = new List<string>
                {
                    ".ai", 
                    ".asx", 
                    ".avi", 
                    ".bmp", 
                    ".csv", 
                    ".dat", 
                    ".doc", 
                    ".docx", 
                    ".epub", 
                    ".fla", 
                    ".flv", 
                    ".gif", 
                    ".html", 
                    ".ico", 
                    ".jpeg", 
                    ".jpg", 
                    ".m4a", 
                    ".mobi", 
                    ".mov", 
                    ".mp3", 
                    ".mp4", 
                    ".mpa", 
                    ".mpg", 
                    ".mpp", 
                    ".pdf", 
                    ".png", 
                    ".pps", 
                    ".ppsx", 
                    ".ppt", 
                    ".pptx", 
                    ".ps", 
                    ".psd", 
                    ".qt", 
                    ".ra", 
                    ".ram", 
                    ".rar", 
                    ".rm", 
                    ".rtf", 
                    ".svg", 
                    ".swf", 
                    ".tif", 
                    ".txt", 
                    ".vcf", 
                    ".vsd", 
                    ".wav", 
                    ".wks", 
                    ".wma", 
                    ".wmv", 
                    ".wps", 
                    ".xls", 
                    ".xlsx", 
                    ".xml", 
                    ".zip"
                };
                _imgExtensions = new List<string> { ".jpg", ".png", ".jpeg", ".gif", ".bmp" };
            }
        }

        private readonly string _filemanagerPath = WebConfigurationManager.AppSettings["Filemanager_Path"] ??
                                                   "/Scripts/filemanager/";

        private readonly string _configPath = WebConfigurationManager.AppSettings["Filemanager_ConfigPath"] ??
                                              "~/Scripts/filemanager/scripts/filemanager.config.js";

        /// <summary>
        ///     Root directory for all file uploads [string]
        ///     Set in web.config. E.g. <add key="Filemanager_RootPath" value="/uploads/" />
        /// </summary>
        private readonly string _rootPath; // Root directory for all file uploads [string]

        /// <summary>
        ///     Directory for icons. [string]
        ///     Set in web.config E.g. <add key="Filemanager_IconDirectory" value="/Scripts/filemanager/images/fileicons/" />
        /// </summary>
        private readonly string _iconDirectory; // Icon directory for filemanager. [string]

        /// <summary>
        ///     White list of allowed file extensions
        /// </summary>
        private readonly List<string> _allowedExtensions; // Only allow these extensions to be uploaded

        /// <summary>
        ///     List of image file extensions
        /// </summary>
        private readonly List<string> _imgExtensions;

        // Only allow this image extensions. [string]

        /// <summary>
        ///     Serializer for generating json responses
        /// </summary>
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        /// <summary>
        ///     Process file manager action
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        [Authorize]
        public ActionResult Index(string mode, string path = null)
        {
            Response.ClearHeaders();
            Response.ClearContent();
            Response.Clear();

            try
            {
                switch (mode)
                {
                    case "getinfo":
                        return Content(GetInfo(path), "application/json", Encoding.UTF8);
                    case "getfolder":
                        return Content(GetFolderInfo(path), "application/json", Encoding.UTF8);
                    case "move":
                        string oldPath = Request.QueryString["old"];
                        string newPath = string.Format("{0}{1}/{2}", Request.QueryString["root"], 
                            Request.QueryString["new"], Path.GetFileName(oldPath));
                        return Content(Move(oldPath, newPath), "application/json", Encoding.UTF8);
                    case "rename":
                        return Content(Rename(Request.QueryString["old"], Request.QueryString["new"]), 
                            "application/json", Encoding.UTF8);
                    case "replace":
                        return Content(Replace(Request.Form["newfilepath"]), "text/html", Encoding.UTF8);
                    case "delete":
                        return Content(Delete(path), "application/json", Encoding.UTF8);
                    case "addfolder":
                        return Content(AddFolder(path, Request.QueryString["name"]), "application/json", Encoding.UTF8);
                    case "download":
                        if (System.IO.File.Exists(Server.MapPath(path)) && IsInRootPath(path))
                        {
                            var fi = new FileInfo(Server.MapPath(path));
                            Response.AddHeader("Content-Disposition", 
                                "attachment; filename=" + Server.UrlPathEncode(path));
                            Response.AddHeader("Content-Length", fi.Length.ToString(CultureInfo.InvariantCulture));
                            return File(fi.FullName, "application/octet-stream");
                        }
                        return new HttpNotFoundResult("File not found");
                    case "add":
                        return Content(AddFile(Request.Form["currentpath"]), "text/html", Encoding.UTF8);
                    case "preview":
                        var fi2 = new FileInfo(Server.MapPath(Request.QueryString["path"]));
                        return new FilePathResult(fi2.FullName, "image/" + fi2.Extension.TrimStart('.'));
                    default:
                        return Content(string.Empty);
                }
            }
            catch (HttpException he)
            {
                return Content(Error(he.Message), "application/json", Encoding.UTF8);
            }
        }

        // ===================================================================
        // ========================== END EDIT ===============================
        // ===================================================================       

        /// <summary>
        ///     Is the file an image file
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <returns></returns>
        private bool IsImage(FileInfo fileInfo)
        {
            return _imgExtensions.Contains(Path.GetExtension(fileInfo.FullName).ToLower());
        }

        /// <summary>
        ///     Is the file in the root path?  Don't allow uploads outside the root path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private bool IsInRootPath(string path)
        {
            return path != null && Path.GetFullPath(path).StartsWith(Path.GetFullPath(_rootPath));
        }

        /// <summary>
        ///     Add a file
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string AddFile(string path)
        {
            string response;

            if (Request.Files.Count == 0 || Request.Files[0].ContentLength == 0)
            {
                response = Error("No file provided.");
            }
            else
            {
                if (!IsInRootPath(path))
                {
                    response = Error("Attempt to add file outside root path");
                }
                else
                {
                    HttpPostedFileBase file = Request.Files[0];
                    if (!_allowedExtensions.Contains(Path.GetExtension(file.FileName).ToLower()))
                    {
                        response = Error("Uploaded file type is not allowed.");
                    }
                    else
                    {
                        // Only allow certain characters in file names
                        string baseFileName = Regex.Replace(Path.GetFileNameWithoutExtension(file.FileName), @"[^\w_-]", 
                            string.Empty);
                        string filePath = Path.Combine(path, baseFileName + Path.GetExtension(file.FileName));

                        // Make file name unique
                        int i = 0;
                        while (System.IO.File.Exists(Server.MapPath(filePath)))
                        {
                            i = i + 1;
                            baseFileName = Regex.Replace(baseFileName, @"_[\d]+$", string.Empty);
                            filePath = Path.Combine(path, baseFileName + "_" + i + Path.GetExtension(file.FileName));
                        }
                        file.SaveAs(Server.MapPath(filePath));

                        response = _json.Serialize(new
                        {
                            Path = path, 
                            Name = Path.GetFileName(file.FileName), 
                            Error = "No error", 
                            Code = 0
                        });
                    }
                }
            }
            return "<textarea>" + response + "</textarea>";
        }

        /// <summary>
        ///     Add a folder
        /// </summary>
        /// <param name="path"></param>
        /// <param name="newFolder"></param>
        /// <returns></returns>
        private string AddFolder(string path, string newFolder)
        {
            if (!IsInRootPath(path))
            {
                return Error("Attempt to add folder outside root path");
            }

            var sb = new StringBuilder();
            Directory.CreateDirectory(Path.Combine(Server.MapPath(path), newFolder));

            sb.AppendLine("{");
            sb.AppendLine("\"Parent\": \"" + path + "\",");
            sb.AppendLine("\"Name\": \"" + newFolder + "\",");
            sb.AppendLine("\"Error\": \"No error\",");
            sb.AppendLine("\"Code\": 0");
            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        ///     Delete a file
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string Delete(string path)
        {
            if (!IsInRootPath(path))
            {
                return Error("Attempt to delete file outside root path");
            }
            if (!System.IO.File.Exists(Server.MapPath(path)) && !Directory.Exists(Server.MapPath(path)))
            {
                return Error("File not found");
            }

            FileAttributes attr = System.IO.File.GetAttributes(Server.MapPath(path));

            var sb = new StringBuilder();

            if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
            {
                Directory.Delete(Server.MapPath(path), true);
            }
            else
            {
                System.IO.File.Delete(Server.MapPath(path));
            }

            sb.AppendLine("{");
            sb.AppendLine("\"Error\": \"No error\",");
            sb.AppendLine("\"Code\": 0,");
            sb.AppendLine("\"Path\": \"" + path + "\"");
            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        ///     Generate json for error message
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        private string Error(string msg)
        {
            return _json.Serialize(new
            {
                Error = msg, 
                Code = -1
            });
        }

        /// <summary>
        ///     Get folder information
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string GetFolderInfo(string path)
        {
            if (!IsInRootPath(path))
            {
                return Error("Attempt to view files outside root path");
            }
            if (!Directory.Exists(Server.MapPath(path)))
            {
                return Error("Directory not found");
            }

            var rootDirInfo = new DirectoryInfo(Server.MapPath(path));
            var sb = new StringBuilder();

            sb.AppendLine("{");

            int i = 0;

            foreach (var dirInfo in rootDirInfo.GetDirectories())
            {
                if (i > 0)
                {
                    sb.Append(",");
                    sb.AppendLine();
                }

                sb.AppendLine("\"" + Path.Combine(path, dirInfo.Name) + "\": {");
                sb.AppendLine("\"Path\": \"" + Path.Combine(path, dirInfo.Name) + "/\",");
                sb.AppendLine("\"Filename\": \"" + dirInfo.Name + "\",");
                sb.AppendLine("\"File Type\": \"dir\",");
                sb.AppendLine("\"Preview\": \"" + _iconDirectory + "_Open.png\",");
                sb.AppendLine("\"Properties\": {");
                sb.AppendLine("\"Date Created\": \"" + dirInfo.CreationTime + "\", ");
                sb.AppendLine("\"Date Modified\": \"" + dirInfo.LastWriteTime + "\", ");
                sb.AppendLine("\"Height\": 0,");
                sb.AppendLine("\"Width\": 0,");
                sb.AppendLine("\"Size\": 0 ");
                sb.AppendLine("},");
                sb.AppendLine("\"Error\": \"\",");
                sb.AppendLine("\"Code\": 0	");
                sb.Append("}");

                i++;
            }

            foreach (var fileInfo in rootDirInfo.GetFiles())
            {
                if (i > 0)
                {
                    sb.Append(",");
                    sb.AppendLine();
                }

                sb.AppendLine("\"" + Path.Combine(path, fileInfo.Name) + "\": {");
                sb.AppendLine("\"Path\": \"" + Path.Combine(path, fileInfo.Name) + "\",");
                sb.AppendLine("\"Filename\": \"" + fileInfo.Name + "\",");
                sb.AppendLine("\"File Type\": \"" + fileInfo.Extension.Replace(".", string.Empty) + "\",");

                if (IsImage(fileInfo))
                {
                    sb.AppendLine("\"Preview\": \"" + Path.Combine(path, fileInfo.Name) + "?" +
                                  fileInfo.LastWriteTime.Ticks + "\",");
                }
                else
                {
                    string icon = String.Format("{0}{1}.png", _iconDirectory, 
                        fileInfo.Extension.Replace(".", string.Empty));
                    if (!System.IO.File.Exists(Server.MapPath(icon)))
                    {
                        icon = String.Format("{0}default.png", _iconDirectory);
                    }
                    sb.AppendLine("\"Preview\": \"" + icon + "\",");
                }

                sb.AppendLine("\"Properties\": {");
                sb.AppendLine("\"Date Created\": \"" + fileInfo.CreationTime + "\", ");
                sb.AppendLine("\"Date Modified\": \"" + fileInfo.LastWriteTime + "\", ");

                if (IsImage(fileInfo))
                {
                    using (Image img = Image.FromFile(fileInfo.FullName))
                    {
                        sb.AppendLine("\"Height\": " + img.Height + ",");
                        sb.AppendLine("\"Width\": " + img.Width + ",");
                    }
                }

                sb.AppendLine("\"Size\": " + fileInfo.Length + " ");
                sb.AppendLine("},");
                sb.AppendLine("\"Error\": \"\",");
                sb.AppendLine("\"Code\": 0	");
                sb.Append("}");

                i++;
            }

            sb.AppendLine();
            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        ///     Get file information
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string GetInfo(string path)
        {
            if (!IsInRootPath(path))
            {
                return Error("Attempt to view file outside root path");
            }
            if (!System.IO.File.Exists(Server.MapPath(path)) && !Directory.Exists(Server.MapPath(path)))
            {
                return Error("File not found");
            }

            var sb = new StringBuilder();

            FileAttributes attr = System.IO.File.GetAttributes(Server.MapPath(path));

            if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
            {
                var dirInfo = new DirectoryInfo(Server.MapPath(path));

                sb.AppendLine("{");
                sb.AppendLine("\"Path\": \"" + path + "\",");
                sb.AppendLine("\"Filename\": \"" + dirInfo.Name + "\",");
                sb.AppendLine("\"File Type\": \"dir\",");
                sb.AppendLine("\"Preview\": \"" + _iconDirectory + "_Open.png\",");
                sb.AppendLine("\"Properties\": {");
                sb.AppendLine("\"Date Created\": \"" + dirInfo.CreationTime + "\", ");
                sb.AppendLine("\"Date Modified\": \"" + dirInfo.LastWriteTime + "\", ");
                sb.AppendLine("\"Height\": 0,");
                sb.AppendLine("\"Width\": 0,");
                sb.AppendLine("\"Size\": 0 ");
                sb.AppendLine("},");
                sb.AppendLine("\"Error\": \"\",");
                sb.AppendLine("\"Code\": 0	");
                sb.AppendLine("}");
            }
            else
            {
                var fileInfo = new FileInfo(Server.MapPath(path));

                sb.AppendLine("{");
                sb.AppendLine("\"Path\": \"" + path + "\",");
                sb.AppendLine("\"Filename\": \"" + fileInfo.Name + "\",");
                sb.AppendLine("\"File Type\": \"" + fileInfo.Extension.Replace(".", string.Empty) + "\",");

                if (IsImage(fileInfo))
                {
                    sb.AppendLine("\"Preview\": \"" + path + "?" + fileInfo.LastWriteTime.Ticks + "\",");
                }
                else
                {
                    sb.AppendLine("\"Preview\": \"" +
                                  String.Format("{0}{1}.png", _iconDirectory, 
                                      fileInfo.Extension.Replace(".", string.Empty)) +
                                  "\",");
                }

                sb.AppendLine("\"Properties\": {");
                sb.AppendLine("\"Date Created\": \"" + fileInfo.CreationTime + "\", ");
                sb.AppendLine("\"Date Modified\": \"" + fileInfo.LastWriteTime + "\", ");

                if (IsImage(fileInfo))
                {
                    using (Image img = Image.FromFile(Server.MapPath(path)))
                    {
                        sb.AppendLine("\"Height\": " + img.Height + ",");
                        sb.AppendLine("\"Width\": " + img.Width + ",");
                    }
                }

                sb.AppendLine("\"Size\": " + fileInfo.Length + " ");
                sb.AppendLine("},");
                sb.AppendLine("\"Error\": \"\",");
                sb.AppendLine("\"Code\": 0	");
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        private string Move(string oldPath, string newPath)
        {
            if (!IsInRootPath(oldPath))
            {
                return Error("Attempt to modify file outside root path");
            }
            if (!IsInRootPath(newPath))
            {
                return Error("Attempt to move a file outside root path");
            }
            if (!System.IO.File.Exists(Server.MapPath(oldPath)) && !Directory.Exists(Server.MapPath(oldPath)))
            {
                return Error("File not found");
            }

            FileAttributes attr = System.IO.File.GetAttributes(Server.MapPath(oldPath));

            var sb = new StringBuilder();

            if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
            {
                var oldDir = new DirectoryInfo(Server.MapPath(oldPath));
                newPath = Path.Combine(newPath, oldDir.Name);
                Directory.Move(Server.MapPath(oldPath), Server.MapPath(newPath));
                var newDir = new DirectoryInfo(Server.MapPath(newPath));

                sb.AppendLine("{");
                sb.AppendLine("\"Error\": \"No error\",");
                sb.AppendLine("\"Code\": 0,");
                sb.AppendLine("\"Old Path\": \"" + oldPath + "\",");
                sb.AppendLine("\"Old Name\": \"" + oldDir.Name + "\",");
                sb.AppendLine("\"New Path\": \"" +
                              newDir.FullName.Replace(HttpRuntime.AppDomainAppPath, "/")
                                  .Replace(Path.DirectorySeparatorChar, '/') + "\",");
                sb.AppendLine("\"New Name\": \"" + newDir.Name + "\"");
                sb.AppendLine("}");
            }
            else
            {
                var oldFile = new FileInfo(Server.MapPath(oldPath));
                var newFile = new FileInfo(Server.MapPath(newPath));
                if (newFile.Extension != oldFile.Extension)
                {
                    // Don't allow extension to be changed
                    newFile = new FileInfo(Path.ChangeExtension(newFile.FullName, oldFile.Extension));
                }
                System.IO.File.Move(oldFile.FullName, newFile.FullName);

                sb.AppendLine("{");
                sb.AppendLine("\"Error\": \"No error\",");
                sb.AppendLine("\"Code\": 0,");
                sb.AppendLine("\"Old Path\": \"" + oldPath.Replace(oldFile.Name, string.Empty) + "\",");
                sb.AppendLine("\"Old Name\": \"" + oldFile.Name + "\",");
                sb.AppendLine("\"New Path\": \"" +
                              newFile.FullName.Replace(HttpRuntime.AppDomainAppPath, "/")
                                  .Replace(Path.DirectorySeparatorChar, '/') + "\",")
                    .Replace(newFile.Name, string.Empty);
                sb.AppendLine("\"New Name\": \"" + newFile.Name + "\"");
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        /// <summary>
        ///     Rename a file or directory
        /// </summary>
        /// <param name="path"></param>
        /// <param name="newName"></param>
        /// <returns></returns>
        private string Rename(string path, string newName)
        {
            if (!IsInRootPath(path))
            {
                return Error("Attempt to modify file outside root path");
            }
            if (!System.IO.File.Exists(Server.MapPath(path)) && !Directory.Exists(Server.MapPath(path)))
            {
                return Error("File not found");
            }

            FileAttributes attr = System.IO.File.GetAttributes(Server.MapPath(path));

            var sb = new StringBuilder();

            if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
            {
                var oldDir = new DirectoryInfo(Server.MapPath(path));
                Directory.Move(Server.MapPath(path), Path.Combine(oldDir.Parent.FullName, newName));
                var newDir = new DirectoryInfo(Path.Combine(oldDir.Parent.FullName, newName));

                sb.AppendLine("{");
                sb.AppendLine("\"Error\": \"No error\",");
                sb.AppendLine("\"Code\": 0,");
                sb.AppendLine("\"Old Path\": \"" + path + "\",");
                sb.AppendLine("\"Old Name\": \"" + oldDir.Name + "\",");
                sb.AppendLine("\"New Path\": \"" +
                              newDir.FullName.Replace(HttpRuntime.AppDomainAppPath, "/")
                                  .Replace(Path.DirectorySeparatorChar, '/') + "\",");
                sb.AppendLine("\"New Name\": \"" + newDir.Name + "\"");
                sb.AppendLine("}");
            }
            else
            {
                var oldFile = new FileInfo(Server.MapPath(path));

                // Don't allow extension to be changed
                newName = Path.GetFileNameWithoutExtension(newName) + oldFile.Extension;
                var newFile = new FileInfo(Path.Combine(oldFile.Directory.FullName, newName));
                System.IO.File.Move(oldFile.FullName, newFile.FullName);

                sb.AppendLine("{");
                sb.AppendLine("\"Error\": \"No error\",");
                sb.AppendLine("\"Code\": 0,");
                sb.AppendLine("\"Old Path\": \"" + path + "\",");
                sb.AppendLine("\"Old Name\": \"" + oldFile.Name + "\",");
                sb.AppendLine("\"New Path\": \"" +
                              newFile.FullName.Replace(HttpRuntime.AppDomainAppPath, "/")
                                  .Replace(Path.DirectorySeparatorChar, '/') + "\",");
                sb.AppendLine("\"New Name\": \"" + newFile.Name + "\"");
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        /// <summary>
        ///     Replace a file
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string Replace(string path)
        {
            if (Request.Files.Count == 0 || Request.Files[0].ContentLength == 0)
            {
                return Error("No file provided.");
            }
            if (!IsInRootPath(path))
            {
                return Error("Attempt to replace file outside root path");
            }
            var fi = new FileInfo(Server.MapPath(path));
            HttpPostedFileBase file = Request.Files[0];
            if (!_allowedExtensions.Contains(Path.GetExtension(file.FileName).ToLower()))
            {
                return Error("Uploaded file type is not allowed.");
            }
            if (!Path.GetExtension(file.FileName).Equals(fi.Extension))
            {
                return Error("Replacement file must have the same extension as the file being replaced.");
            }
            if (!fi.Exists)
            {
                return Error("File to replace not found.");
            }
            file.SaveAs(fi.FullName);

            return "<textarea>" + _json.Serialize(new
            {
                Path = path.Replace("/" + fi.Name, string.Empty), 
                fi.Name, 
                Error = "No error", 
                Code = 0
            }) + "</textarea>";
        }
    }
}