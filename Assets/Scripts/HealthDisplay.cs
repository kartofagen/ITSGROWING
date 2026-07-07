using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class HealthDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI healthText;
    
    // Цвета для разных типов веток
    [SerializeField] private Color attackerColor = Color.white;
    [SerializeField] private Color defenderColor = new Color(0.8f, 0.2f, 0.8f, 1f); // Фиолетовый
    [SerializeField] private Color eaterColor = Color.yellow;
    [SerializeField] private Color textColor = Color.red; // Красный для остального текста
    
    private Dictionary<System.Type, Color> typeColors = new Dictionary<System.Type, Color>();
    
    void Start()
    {
        if (healthText == null)
        {
            healthText = GetComponent<TextMeshProUGUI>();
        }
        
        // Инициализируем цвета для типов
        typeColors[typeof(Lead)] = attackerColor;
        typeColors[typeof(Bass)] = defenderColor;
        typeColors[typeof(EatSounds)] = eaterColor;
    }
    
    void Update()
    {
        if (BranchHealthManager.Instance == null || healthText == null) return;
        
        var branches = BranchHealthManager.Instance.GetBranches();
        if (branches == null || branches.Count == 0) return;
        
        // Получаем здоровье для каждой ветки по типу компонента
        int attackerHealth = 0;
        int defenderHealth = 0;
        int eaterHealth = 0;
        
        foreach (var branch in branches)
        {
            if (branch.instrumentScript is Lead)
                attackerHealth = branch.currentHealth;
            else if (branch.instrumentScript is Bass)
                defenderHealth = branch.currentHealth;
            else if (branch.instrumentScript is EatSounds)
                eaterHealth = branch.currentHealth;
        }
        
        // Формируем строку с форматированием цветов
        string formattedText = FormatHealthText(attackerHealth, defenderHealth, eaterHealth);
        healthText.text = formattedText;
    }
    
    private string FormatHealthText(int attacker, int defender, int eater)
    {
        // Конвертируем цвета в hex формат для TMPro
        string redHex = ColorUtility.ToHtmlStringRGB(textColor);
        string whiteHex = ColorUtility.ToHtmlStringRGB(attackerColor);
        string purpleHex = ColorUtility.ToHtmlStringRGB(defenderColor);
        string yellowHex = ColorUtility.ToHtmlStringRGB(eaterColor);
        
        // Собираем форматированную строку
        return $"<color=#{redHex}>Health:   {attacker + defender + eater} = </color>" +
               $"<color=#{whiteHex}>{attacker}</color>" +
               $"<color=#{redHex}> + </color>" +
               $"<color=#{purpleHex}>{defender}</color>" +
               $"<color=#{redHex}> + </color>" +
               $"<color=#{yellowHex}>{eater}</color>";
    }
    
    // Альтернативный вариант: используем встроенные имена цветов TMPro (менее точные)
    private string FormatHealthTextSimple(int attacker, int defender, int eater)
    {
        return "<color=red>Health = </color>" +
               $"<color=white>{attacker}</color>" +
               "<color=red> + </color>" +
               $"<color=purple>{defender}</color>" +
               "<color=red> + </color>" +
               $"<color=yellow>{eater}</color>";
    }
}