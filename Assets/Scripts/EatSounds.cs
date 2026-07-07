using UnityEngine;
using System.Collections.Generic;

public class EatSounds : InstrumentBase
{
    [Header("MyceliumEater Configuration")]
    [Tooltip("Префаб MyceliumEater для спавна")]
    public GameObject myceliumEaterPrefab;
    
    [Tooltip("Максимальное количество одновременно активных Eater'ов по уровням апгрейда")]
    public List<int> maxEatersPerLevel = new List<int>() { 1, 1, 2, 3, 4 };
    
    [Tooltip("Скорость роста (GrowSpeed) по уровням апгрейда")]
    public List<float> growSpeedPerLevel = new List<float>() { 0.25f, 0.35f, 0.5f, 0.7f, 1.0f };
    
    [Header("Default Eater")]
    [Tooltip("Дефолтный MyceliumEater в детях (должен быть заранее создан)")]
    public MyceliumEater defaultEater;
    
    private BranchHealth branchHealth;
    private int currentLevel = 0;
    private int currentMaxEaters = 1;
    private float currentGrowSpeed = 0.25f;
    
    private List<GameObject> activeEaters = new List<GameObject>();
    private Queue<GameObject> targetQueue = new Queue<GameObject>();
    
    private bool isDefaultEaterActive = true;
    
    void Start()
    {
        branchHealth = GetComponent<BranchHealth>();
        if (branchHealth == null)
        {
            Debug.LogWarning("EatSounds: No BranchHealth component found!");
        }
        
        if (defaultEater == null)
        {
            Debug.LogWarning("EatSounds: Default eater not assigned!");
        }
        else
        {
            // Запускаем рост дефолтного мицелия при старте
            defaultEater.isDefault = true;
            defaultEater.Grow();
            isDefaultEaterActive = true;
        }
        
        OnUpgradeLevelChanged(0);
        
        // Добавляем коллайдер, если его нет
        if (GetComponent<Collider>() == null)
        {
            Debug.LogWarning("EatSounds: No Collider component found! Adding a SphereCollider with isTrigger.");
            SphereCollider collider = gameObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 10f; // Дефолтный радиус
        }
        else
        {
            // Проверяем, что коллайдер является триггером
            Collider collider = GetComponent<Collider>();
            if (!collider.isTrigger)
            {
                Debug.LogWarning("EatSounds: Existing Collider is not a trigger! Setting isTrigger to true.");
                collider.isTrigger = true;
            }
        }
    }
    
    void Update()
    {
        // Обработка очереди целей
        ProcessTargetQueue();
        
        // Очистка списка от уничтоженных eater'ов
        activeEaters.RemoveAll(e => e == null);
    }
    
    // Обработка входа в триггер
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            EnemyHealth eh = other.GetComponent<EnemyHealth>();
            if (eh != null && eh.IsDead && !eh.isEaten)
            {
                // Добавляем в очередь, если ещё не добавлен
                if (!targetQueue.Contains(other.gameObject) && !IsTargetBeingEaten(other.gameObject))
                {
                    targetQueue.Enqueue(other.gameObject);
                    eh.isEaten = true; // Помечаем как "в процессе"
                    Debug.Log($"Dead enemy detected in trigger: {other.gameObject.name}");
                }
            }
        }
    }
    
    // Обработка входа в 2D триггер (если используется 2D физика)
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            EnemyHealth eh = other.GetComponent<EnemyHealth>();
            if (eh != null && eh.IsDead && !eh.isEaten)
            {
                // Добавляем в очередь, если ещё не добавлен
                if (!targetQueue.Contains(other.gameObject) && !IsTargetBeingEaten(other.gameObject))
                {
                    targetQueue.Enqueue(other.gameObject);
                    eh.isEaten = true; // Помечаем как "в процессе"
                    Debug.Log($"Dead enemy detected in 2D trigger: {other.gameObject.name}");
                }
            }
        }
    }
    
    public void OnUpgradeLevelChanged(int newLevel)
    {
        currentLevel = newLevel;
        
        // Получаем параметры для текущего уровня
        currentMaxEaters = GetMaxEatersForLevel(newLevel);
        currentGrowSpeed = GetGrowSpeedForLevel(newLevel);
        
        Debug.Log($"EatSounds upgraded: Level {newLevel}, MaxEaters={currentMaxEaters}, GrowSpeed={currentGrowSpeed}");
    }
    
    private bool IsTargetBeingEaten(GameObject target)
    {
        foreach (var eater in activeEaters)
        {
            if (eater != null)
            {
                MyceliumTree3D tree = eater.GetComponent<MyceliumTree3D>();
                if (tree != null && tree.targetObject != null && tree.targetObject.gameObject == target)
                {
                    return true;
                }
            }
        }
        return false;
    }
    
    private void ProcessTargetQueue()
    {
        // Проверяем, можем ли мы создать новый eater
        if (targetQueue.Count > 0 && activeEaters.Count < currentMaxEaters)
        {
            GameObject target = targetQueue.Dequeue();
            
            // Проверяем, что цель всё ещё существует
            if (target != null)
            {
                SpawnEaterForTarget(target);
            }
        }
    }
    
    private void SpawnEaterForTarget(GameObject target)
    {
        if (myceliumEaterPrefab == null)
        {
            Debug.LogError("EatSounds: MyceliumEater prefab not assigned!");
            return;
        }
        
        // Если активен дефолтный eater, сначала его убираем
        if (isDefaultEaterActive && defaultEater != null)
        {
            isDefaultEaterActive = false;
            defaultEater.Shrink(() => {
                // После завершения обратного роста спавним префаб
                SpawnEaterPrefab(target);
            });
        }
        else
        {
            SpawnEaterPrefab(target);
        }
    }
    
    private void SpawnEaterPrefab(GameObject target)
    {
        GameObject eaterObj = Instantiate(myceliumEaterPrefab);
        
        MyceliumEater eater = eaterObj.GetComponent<MyceliumEater>();
        MyceliumTree3D tree = eaterObj.GetComponent<MyceliumTree3D>();
        MyceliumGrowController growController = eaterObj.GetComponent<MyceliumGrowController>();
        
        if (tree != null && target != null)
        {
            tree.targetObject = target.transform;
            tree.targetAttractionStrength = 0.9f; // Сильное притяжение к цели
            tree.Rebuild(); // Перестраиваем дерево с новой целью
        }
        
        if (growController != null)
        {
            growController.growSpeed = currentGrowSpeed;
        }
        
        if (eater != null)
        {
            eater.isDefault = false;
            eater.growSpeed = currentGrowSpeed;
            eater.targetScale = 1f;
            eater.onDone = OnEaterDone;
            eater.Grow();
        }
        
        activeEaters.Add(eaterObj);
    }
    
    private void OnEaterDone(GameObject eaterObj)
    {
        activeEaters.Remove(eaterObj);
        Destroy(eaterObj);
        
        // Если нет активных eater'ов, возвращаем дефолтный
        if (activeEaters.Count == 0 && defaultEater != null && !isDefaultEaterActive)
        {
            isDefaultEaterActive = true;
            defaultEater.ResetToStart();
            defaultEater.Grow();
        }
    }
    
    private int GetMaxEatersForLevel(int level)
    {
        if (level >= 0 && level < maxEatersPerLevel.Count)
            return maxEatersPerLevel[level];
        
        if (level >= maxEatersPerLevel.Count && maxEatersPerLevel.Count > 0)
            return maxEatersPerLevel[maxEatersPerLevel.Count - 1];
            
        return 1;
    }
    
    private float GetGrowSpeedForLevel(int level)
    {
        if (level >= 0 && level < growSpeedPerLevel.Count)
            return growSpeedPerLevel[level];
        
        if (level >= growSpeedPerLevel.Count && growSpeedPerLevel.Count > 0)
            return growSpeedPerLevel[growSpeedPerLevel.Count - 1];
            
        return 0.25f;
    }
}