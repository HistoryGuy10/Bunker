using TMPro;
using UnityEngine;

public class TMPTXT : MonoBehaviour
{
    [SerializeField] TMP_Text textComponent;
    public void SetText(string text)
    {
        textComponent.text = text;
    }
}
