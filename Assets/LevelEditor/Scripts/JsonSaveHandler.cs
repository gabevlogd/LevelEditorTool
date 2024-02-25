using System.IO;
using UnityEditor;
using UnityEngine;

public class JsonSaveHandler
{
    public static void Save(string saveFolderName, string saveFileName, object saveData)
    {
        if (saveData == null || saveFolderName == null || saveFileName == null)
        {
            Debug.LogError($"Invalid parameters");    
            return;
        }


        string savePath = FindSaveFolder(saveFolderName);
        if (savePath == null)
            savePath = CreateSaveFolder(saveFolderName);

        string savePlayerData = JsonUtility.ToJson(saveData);
        File.WriteAllText($"{savePath}/{saveFileName}.json", savePlayerData);
        Debug.Log($"Data saved at {savePath}/{saveFileName}.json");
    }

    public static TLoadData Load<TLoadData>(string loadFileName)
    {
        if (loadFileName == null)
        {
            Debug.LogError("Invalid file name");
            return default(TLoadData);
        }

        string loadFile = File.ReadAllText(FindSaveFile(loadFileName));
        if (loadFile != null)
            return JsonUtility.FromJson<TLoadData>(loadFile);
        else return default(TLoadData);

    }

    private static string FindSaveFile(string saveFileName)
    {
        string path = Application.dataPath + "/";
        saveFileName += ".json";

        string[] fileArray = Directory.GetFiles(path, saveFileName, SearchOption.AllDirectories);

        if (fileArray.Length > 0)
        {
            return fileArray[0].Replace('\\', '/');
        }
        else
        {
            Debug.LogWarning($"File {saveFileName} not found");
            return null;
        }
    }

    private static string FindSaveFolder(string folderName)
    {
        string assetsPath = Application.dataPath;
        string[] folders = Directory.GetDirectories(assetsPath, folderName, SearchOption.AllDirectories);

        if (folders.Length > 0)
        {
            return folders[0].Replace('\\', '/');
        }
        else
        {
            Debug.LogWarning($"Folder {folderName} not found");
            return null;
        }
    }

    private static string CreateSaveFolder(string saveFolderName)
    {
        string savePath;
        AssetDatabase.CreateFolder("Assets", saveFolderName);
        savePath = $"{Application.dataPath}/{saveFolderName}";
        Debug.Log($"Folder {saveFolderName} created at {Application.dataPath}");
        return savePath;
    }

}
