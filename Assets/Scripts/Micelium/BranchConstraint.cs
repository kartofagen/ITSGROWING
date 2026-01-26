using UnityEngine;

/// <summary>
/// Базовый класс для ограничений роста веток.
/// </summary>
public abstract class BranchConstraint
{
    /// <summary>
    /// Результат применения ограничения.
    /// </summary>
    public struct ConstraintResult
    {
        public Vector3 newPosition;
        public Vector3 newDirection;
        public bool shouldStop;
        public bool wasViolated;
    }

    /// <summary>
    /// Применяет ограничение к позиции и направлению.
    /// </summary>
    /// <param name="currentPos">Текущая позиция</param>
    /// <param name="nextPos">Следующая позиция (до применения ограничения)</param>
    /// <param name="direction">Текущее направление роста</param>
    /// <returns>Результат применения ограничения</returns>
    public abstract ConstraintResult Apply(Vector3 currentPos, Vector3 nextPos, Vector3 direction);
}
