using System.Collections;
using UnityEngine;

public class DelayedComponentEnabler : MonoBehaviour
{
    [Header("Настройки")]
    [SerializeField] private Behaviour _componentToEnable; // Целевой компонент для активации
    [SerializeField] private float _delayInSeconds = 2f;   // Задержка в секундах
    
    [Header("Автостарт")]
    [SerializeField] private bool _enableOnStart = true;   // Автоматически запустить при старте
    
    private void Start()
    {
        if (_enableOnStart)
        {
            StartDelayedEnable();
        }
    }
    
    /// <summary>
    /// Запустить отложенную активацию компонента
    /// </summary>
    public void StartDelayedEnable()
    {
        if (_componentToEnable == null)
        {
            Debug.LogWarning("Компонент для активации не назначен!", this);
            return;
        }
        
        StartCoroutine(EnableComponentAfterDelay());
    }
    
    /// <summary>
    /// Запустить отложенную активацию с кастомной задержкой
    /// </summary>
    public void StartDelayedEnable(float customDelay)
    {
        if (_componentToEnable == null)
        {
            Debug.LogWarning("Компонент для активации не назначен!", this);
            return;
        }
        
        StartCoroutine(EnableComponentAfterDelay(customDelay));
    }
    
    /// <summary>
    /// Корутина для активации компонента через задержку
    /// </summary>
    private IEnumerator EnableComponentAfterDelay()
    {
        yield return new WaitForSeconds(_delayInSeconds);
        _componentToEnable.enabled = true;
        Debug.Log($"Компонент {_componentToEnable.GetType().Name} активирован!");
    }
    
    /// <summary>
    /// Перегруженная версия с кастомной задержкой
    /// </summary>
    private IEnumerator EnableComponentAfterDelay(float customDelay)
    {
        yield return new WaitForSeconds(customDelay);
        _componentToEnable.enabled = true;
        Debug.Log($"Компонент {_componentToEnable.GetType().Name} активирован!");
    }
    
    /// <summary>
    /// Остановить отложенную активацию
    /// </summary>
    public void StopDelayedEnable()
    {
        StopAllCoroutines();
        Debug.Log("Отложенная активация остановлена");
    }
}