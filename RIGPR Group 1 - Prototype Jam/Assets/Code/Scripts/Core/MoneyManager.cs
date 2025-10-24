using UnityEngine;
using TMPro;

public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance;

    [SerializeField] private TextMeshProUGUI moneyText;

    private float totalMoney = 0f;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        UpdateUI();
    }

    public void AddMoney(float amount)
    {
        totalMoney += amount;
        UpdateUI();
    }

    private void UpdateUI()
    {
        moneyText.text = "£" + totalMoney.ToString("F0");
    }

    public void ResetMoney()
    {
        totalMoney = 0;
        UpdateUI();
    }
}
