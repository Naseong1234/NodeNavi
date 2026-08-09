using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Inspector에 설정한 씬 이름으로 화면을 전환합니다.
/// UI Button의 On Click 이벤트에서 LoadConfiguredScene을 연결해 사용합니다.
/// </summary>
public class sh_SceneController : MonoBehaviour
{

    /// <summary>
    /// sceneName 필드에 설정된 씬을 불러옵니다.
    /// </summary>
    public void SceneChange(string sceneName)
    {
       
        SceneManager.LoadScene(sceneName);
    }
}
