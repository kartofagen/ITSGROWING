using UnityEngine;
using System.Collections.Generic;

public class BranchFollow : MonoBehaviour
{
    [System.Serializable]
    public class Branch
    {
        public Transform branchTransform;
        public float returnSpeed = 8f;        // Скорость возврата к начальному углу
        public float inertiaStrength = 1f;    // Сила инерции
        public float damping = 0.9f;          // Затухание колебаний
        public float maxOffset = 30f;         // Максимальное отклонение
        
        [HideInInspector] public float currentOffset = 0f;
        [HideInInspector] public float offsetVelocity = 0f;
        [HideInInspector] public float initialLocalAngle = 0f; // Начальный относительный угол
    }
    
    [Header("Ветки")]
    [SerializeField] private List<Branch> branches = new List<Branch>();
    
    [Header("Настройки инерции")]
    [SerializeField] private float globalInertiaMultiplier = 1f;
    [SerializeField] private float angularVelocityThreshold = 0.1f;
    
    private Transform mainObject;
    private float previousMainAngle = 0f;
    private float mainAngularVelocity = 0f;
    
    void Start()
    {
        mainObject = transform;
        previousMainAngle = mainObject.eulerAngles.z;
        
        // Сохраняем начальные относительные углы
        foreach (var branch in branches)
        {
            if (branch.branchTransform != null)
            {
                // Вычисляем начальный относительный угол (локальный угол относительно основного объекта)
                branch.initialLocalAngle = Mathf.DeltaAngle(
                    mainObject.eulerAngles.z, 
                    branch.branchTransform.eulerAngles.z
                );
            }
        }
    }
    
    void Update()
    {
        // Вычисляем угловую скорость основного объекта
        CalculateAngularVelocity();
        
        // Обновляем каждую ветку
        for (int i = 0; i < branches.Count; i++)
        {
            UpdateBranch(i);
        }
    }
    
    void CalculateAngularVelocity()
    {
        float currentAngle = mainObject.eulerAngles.z;
        float deltaAngle = Mathf.DeltaAngle(previousMainAngle, currentAngle);
        mainAngularVelocity = deltaAngle / Time.deltaTime;
        previousMainAngle = currentAngle;
    }
    
    void UpdateBranch(int index)
    {
        var branch = branches[index];
        if (branch.branchTransform == null) return;
        
        // 1. Применяем инерцию от вращения основного объекта
        if (Mathf.Abs(mainAngularVelocity) > angularVelocityThreshold)
        {
            // Сила инерции пропорциональна угловой скорости
            float inertiaForce = mainAngularVelocity * branch.inertiaStrength * globalInertiaMultiplier;
            branch.offsetVelocity += inertiaForce * Time.deltaTime;
        }
        
        // 2. Пружинная система: возвращаем к нулю с затуханием
        float springForce = -branch.currentOffset * branch.returnSpeed;
        branch.offsetVelocity += springForce * Time.deltaTime;
        branch.offsetVelocity *= branch.damping;
        
        // 3. Интегрируем скорость для получения смещения
        branch.currentOffset += branch.offsetVelocity * Time.deltaTime;
        
        // 4. Ограничиваем максимальное смещение
        branch.currentOffset = Mathf.Clamp(branch.currentOffset, -branch.maxOffset, branch.maxOffset);
        
        // 5. Вычисляем итоговый угол ветки
        // Основной угол + начальный относительный угол + текущее смещение от инерции
        float targetAngle = mainObject.eulerAngles.z + branch.initialLocalAngle + branch.currentOffset;
        
        // 6. Плавно применяем поворот
        Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle);
        branch.branchTransform.rotation = Quaternion.Slerp(
            branch.branchTransform.rotation,
            targetRotation,
            10f * Time.deltaTime // Скорость коррекции поворота
        );
    }
}