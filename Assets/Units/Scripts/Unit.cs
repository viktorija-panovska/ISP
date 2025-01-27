using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Populous
{
    /// <summary>
    /// The <c>Unit</c> class is a <c>MonoBehavior</c> which represents and handles the functioning of one unit.
    /// Units are an abstraction of the population of the world, where one unit represents a group of people.
    /// </summary>
    public class Unit : NetworkBehaviour, IInspectableObject
    {
        [SerializeField] private GameObject[] m_LeaderSigns;
        [SerializeField] private GameObject m_KnightSword;
        [SerializeField] private float m_SecondsUntilEnteringEnabled = 0.5f;
        [SerializeField] private GameObject m_Highlight;
        [SerializeField] private GameObject m_MinimapIcon;

        [Header("Detectors")]
        [SerializeField] private UnitCloseRangeDetector m_CloseRangeDetector;
        [SerializeField] private UnitMidRangeDetector m_MidRangeDetector;
        [SerializeField] private UnitWideRangeDetector m_WideRangeDetector;
        [SerializeField] private int m_CloseRangeRadius = 15;
        [SerializeField] private int m_MidRangeTilesPerSide = 1;
        [SerializeField] private int m_WideRangeTilesPerSide = 20;

        public GameObject GameObject { get => gameObject; }
        private UnitMovementHandler m_MovementHandler;

        /// <summary>
        /// The <c>TerrainPoint</c> on the terrain grid which is closest to the current position of the unit.
        /// </summary>
        public TerrainPoint ClosestMapPoint { get => new(gameObject.transform.position.x, gameObject.transform.position.z, getClosestPoint: true); }

        /// <summary>
        /// The tile the unit is currently on.
        /// </summary>
        public TerrainPoint Tile { get => new(gameObject.transform.position.x, gameObject.transform.position.z, getClosestPoint: false); }

        private Team m_Team;
        /// <summary>
        /// Gets the team this unit belongs to.
        /// </summary>
        public Team Team { get => m_Team; }

        private UnitClass m_Class;
        /// <summary>
        /// Gets the class of this unit.
        /// </summary>
        public UnitClass Class { get => m_Class; }

        private UnitBehavior m_Behavior;
        /// <summary>
        /// Gets the current state of this unit.
        /// </summary>
        public UnitBehavior Behavior { get => m_Behavior; }

        private int m_Strength;
        /// <summary>
        /// The current strength of the unit.
        /// </summary>
        /// <remarks>The strength of the unit is the number of walkers represented by this one unit.</remarks>
        public int Strength { get => m_Strength; }

        private bool m_IsInFight;
        /// <summary>
        /// True if the unit is currently in a fight with a unit of the opposite team, false otherwise.
        /// </summary>
        public bool IsInFight { get => m_IsInFight; }

        private int m_FightId = -1;
        /// <summary>
        /// Gets the idenitifier of the fight the unit is involved in.
        /// </summary>
        /// <remarks>-1 if the unit is not involved in a fiight.</remarks>
        public int FightId { get => m_FightId; }

        private bool m_CanEnterSettlement;
        /// <summary>
        /// True if the followers from the unit can enter a settlement, false otherwise.
        /// </summary>
        public bool CanEnterSettlement { get => m_CanEnterSettlement; }

        private NetworkVariable<bool> m_IsInspected = new();
        public bool IsInspected { get => m_IsInspected.Value; set => m_IsInspected.Value = value; }


        /// <summary>
        /// Sets up the properties and different components of the unit.
        /// </summary>
        /// <param name="team">The <c>Team</c> the unit belongs to.</param>
        /// <param name="strength">The starting maxStrength of the unit.</param>
        public void Setup(Team team, int strength, bool canEnterSettlement)
        {
            m_Team = team;
            m_Strength = team == Team.BLUE ? 2 : 1/*strength*/;
            m_CanEnterSettlement = canEnterSettlement;

            SetupMinimapIcon();

            m_MovementHandler = GetComponent<UnitMovementHandler>();
            m_MovementHandler.InitializeMovement();

            m_CloseRangeDetector.Setup(this);
            m_WideRangeDetector.Setup(this, m_WideRangeTilesPerSide);
            m_MidRangeDetector.Setup(this, m_MidRangeTilesPerSide);

            if (m_CanEnterSettlement == false)
                StartCoroutine(WaitToEnableEntering());
        }


        #region Behavior and Class

        /// <summary>
        /// Sets the current class of the unit to the given class.
        /// </summary>
        /// <param name="unitClass">The <c>UnitClass</c> that should be set.</param>
        public void SetClass(UnitClass unitClass)
        {
            if (m_Class == unitClass) return;

            if (m_Class == UnitClass.LEADER)
                ToggleLeaderSign(false);

            if (m_Class == UnitClass.KNIGHT)
                ToggleKnightSword(false);

            m_Class = unitClass;

            if (m_Class == UnitClass.LEADER)
                ToggleLeaderSign(true);

            if (m_Class == UnitClass.KNIGHT)
            {
                SetBehavior(UnitBehavior.FIGHT);
                ToggleKnightSword(true);
                m_WideRangeDetector.SetDetectorSize(Terrain.Instance.TilesPerSide);
            }

            if (IsInspected)
                GameController.Instance.UpdateInspectedUnit(this, updateClass: true);
        }

        /// <summary>
        /// Sets the current state of the unit to the given state.
        /// </summary>
        /// <param name="unitBehavior">The <c>UnitBehavior</c> that should be set.</param>
        public void SetBehavior(UnitBehavior unitBehavior)
        {
            if (m_Behavior == unitBehavior) return;

            m_Behavior = unitBehavior;

            m_MidRangeDetector.StateChange(unitBehavior);
            m_WideRangeDetector.BehaviorChange(unitBehavior);
            m_MovementHandler.SetRoam();

            UnitManager.Instance.ResetGridSteps(m_Team);
        }

        /// <summary>
        /// Activates or deactivates the sign the leader should be holding.
        /// </summary>
        /// <param name="isOn">True if the sign should be activated, false otherwise.</param>
        private void ToggleLeaderSign(bool isOn)
        {
            m_LeaderSigns[(int)m_Team].SetActive(isOn);
            //m_TeamSymbols[(int)m_Team].GetComponent<ObjectActivator>().SetActiveClientRpc(isOn);
        }

        /// <summary>
        /// Activates or deactivates the sword a knight should be holding.
        /// </summary>
        /// <param name="isOn">True if the sword should be activated, false otherwise.</param>
        private void ToggleKnightSword(bool isOn)
        {
            m_KnightSword.SetActive(isOn);
            //m_KnightSword.GetComponent<ObjectActivator>().SetActiveClientRpc(isOn);
        }

        /// <summary>
        /// Waits for a number seconds before enabling the unit to enter settlements.
        /// </summary>
        /// <remarks>This is to avoid a unit released from a settlement immediately attempting to reenter the settlement.</remarks>
        /// <returns>An <c>IEnumerator</c> that waits for a number of seconds.</returns>
        private IEnumerator WaitToEnableEntering()
        {
            yield return new WaitForSeconds(m_SecondsUntilEnteringEnabled);
            m_CanEnterSettlement = true;
        }

        #endregion


        #region Strength

        /// <summary>
        /// Checks whether the unit has maximum strength.
        /// </summary>
        /// <returns>True if the unit has maximum strength, false otherwise.</returns>
        public bool HasMaxStrength() => m_Strength == UnitManager.Instance.MaxUnitStrength;

        /// <summary>
        /// Adds the given amount of maxStrength to the unit.
        /// </summary>
        /// <param name="amount">The amount of maxStrength to be added.</param>
        public void GainStrength(int amount)
        {
            m_Strength += amount;

            if (IsInspected)
                GameController.Instance.UpdateInspectedUnit(this, updateStrength: true);
        }

        /// <summary>
        /// Removes the given amount of maxStrength from the unit.
        /// </summary>
        /// <param name="amount">The amount of maxStrength to be removed.</param>
        public void LoseStrength(int amount, bool isDamaged = true) 
        { 
            m_Strength -= amount;

            if (IsInspected)
                GameController.Instance.UpdateInspectedUnit(this, updateStrength: true);

            if (m_Strength == 0)
                UnitManager.Instance.DespawnUnit(gameObject, hasDied: isDamaged);
        }

        #endregion


        #region Fight

        /// <summary>
        /// Enters the unit into a fight.
        /// </summary>
        public void StartFight(int id)
        {
            m_IsInFight = true;
            m_FightId = id;
            m_MovementHandler.Pause(true);
        }

        /// <summary>
        /// Removes the unit from a fight.
        /// </summary>
        public void EndFight()
        {
            m_IsInFight = false;
            m_FightId = -1;
            m_MovementHandler.Pause(false);
        }

        #endregion


        #region Movement

        /// <summary>
        /// Starts or stops the movement of the unit.
        /// </summary>
        /// <param name="pause">True if the movement should be stopped, false otherwise.</param>
        public void ToggleMovement(bool pause) => m_MovementHandler.Pause(pause);

        /// <summary>
        /// Computes the height of the terrain under the current position of the unit and 
        /// sets the position of the unit so that it is standing properly on the terrain.
        /// </summary>
        public void RecomputeHeight()
        {
            float height;

            int startHeight = m_MovementHandler.StartLocation.Y;
            int endHeight = m_MovementHandler.EndLocation.Y;

            if (startHeight == endHeight)
                height = startHeight;
            else
            {
                float heightDifference = Mathf.Abs(endHeight - startHeight);
                float totalDistance = new Vector2(
                    m_MovementHandler.EndLocation.X - m_MovementHandler.StartLocation.X,
                    m_MovementHandler.EndLocation.Z - m_MovementHandler.StartLocation.Z
                ).magnitude;

                float distance = startHeight < endHeight
                    ? new Vector2(transform.position.x - m_MovementHandler.StartLocation.X, transform.position.z - m_MovementHandler.StartLocation.Z).magnitude
                    : new Vector2(m_MovementHandler.EndLocation.X - transform.position.x, m_MovementHandler.EndLocation.Z - transform.position.z).magnitude;

                height = heightDifference * distance / totalDistance;
                height = startHeight < endHeight ? startHeight + height : endHeight + height;
            }

            if (Terrain.Instance.IsTileUnderwater((Tile.GridX, Tile.GridZ)))
                UnitManager.Instance.DespawnUnit(gameObject, hasDied: true);
            else
                SetHeight/*ClientRpc*/(height);

            m_MovementHandler.UpdateTargetPointHeight();
        }

        /// <summary>
        /// Sets the height that the unit stands at.
        /// </summary>
        /// <param name="height">The height the unit should stand at.</param>
        //[ClientRpc]
        private void SetHeight/*ClientRpc*/(float height) => transform.position = new Vector3(transform.position.x, height, transform.position.z);

        /// <summary>
        /// Rotates the unit to face in the given direction.
        /// </summary>
        /// <param name="lookPosition">The direction the unit should be turned towards.</param>
        //[ClientRpc]
        public void Rotate/*ClientRpc*/(Vector3 lookPosition)
        {
            if (lookPosition != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(lookPosition);
        }

        #endregion


        #region Other units

        /// <summary>
        /// Gets a direction the unit should move to run across other units.
        /// </summary>
        /// <returns>A <c>Vector3</c> representing the direction of the other units, 
        /// zero vector if no units are detected in the vicinity.</returns>
        public Vector3 GetEnemyDirection() => m_WideRangeDetector.GetAverageDirection();

        /// <summary>
        /// Gets a unit that this unit can follow.
        /// </summary>
        /// <returns>A <c>Unit</c> to be followed if one is found, null otherwise.</returns>
        public Unit GetUnitInRange()
        {
            GameObject target = m_MidRangeDetector.GetTarget();
            if (!target) return null;
            return target.GetComponent<Unit>();
        }

        public Settlement GetSettlementInRange()
        {
            GameObject target = m_MidRangeDetector.GetTarget();
            if (!target) return null;
            return target.GetComponent<Settlement>();
        }

        /// <summary>
        /// Stops this unit from following its target, if that target is the given <c>GameObject</c>.
        /// </summary>
        /// <param name="target">The <c>GameObject</c> that should be checked against the target.</param>
        public void LoseTarget(GameObject target) 
        {
            Unit unit = target.GetComponent<Unit>();
            if (unit)
            {
                m_MovementHandler.StopFollowingUnit(unit);
                return;
            }

            Settlement settlement = target.GetComponent<Settlement>();
            if (settlement)
                m_MovementHandler.StopMovingToTile(settlement.OccupiedTile);
        }

        public void CheckIfTargetTileFlat()
        {
            if (m_Behavior != UnitBehavior.SETTLE) return;
            m_MovementHandler.CheckIfTargetTileFlat();
        }

        /// <summary>
        /// Reacts to a new unit being assigned as the leader of the team, if this unit isn't the leader.
        /// </summary>
        public void NewLeaderUnitGained()
        {
            if (m_Class == UnitClass.LEADER || m_Behavior != UnitBehavior.GO_TO_MAGNET) return;
            m_MovementHandler.GoToSymbol();
        }

        #endregion


        #region Team symbol

        /// <summary>
        /// Sets a new target for the unit movement if it is going to its faction symbol.
        /// </summary>
        public void SymbolLocationChanged()
        {
            if (m_Behavior != UnitBehavior.GO_TO_MAGNET) return;
            m_MovementHandler.GoToSymbol();
        }

        /// <summary>
        /// Sets unit behavior for when it has reached its faction symbol.
        /// </summary>
        public void TeamSymbolReached()
        {
            if (m_Behavior != UnitBehavior.GO_TO_MAGNET) return;
            m_MovementHandler.SymbolReached = true;
        }

        #endregion


        #region UI

        /// <summary>
        /// Called when the mouse cursor hovers over the unit.
        /// </summary>
        /// <remarks>Called on client.</remarks>
        /// <param name="eventData">Event data for the pointer event.</param>
        public void OnPointerEnter(PointerEventData eventData) => SetHighlight(true);

        /// <summary>
        /// Called when the mouse cursor stops hovering over the unit.
        /// </summary>
        /// <remarks>Called on client.</remarks>
        /// <param name="eventData">Event data for the pointer event.</param>
        public void OnPointerExit(PointerEventData eventData)
        {
            if (m_IsInspected.Value) return;
            SetHighlight(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Called on client.</remarks>
        /// <param name="eventData"></param>
        public void OnPointerClick(PointerEventData eventData)
            => GameController.Instance.SetInspectedObject_ServerRpc(PlayerController.Instance.Team, GetComponent<NetworkObject>());

        public void SetHighlight(bool isActive) => m_Highlight.SetActive(isActive);

        private void SetupMinimapIcon()
        {
            float scale = Terrain.Instance.UnitsPerSide / GameUI.Instance.MinimapIconScale;

            m_MinimapIcon.transform.localScale = new(scale, m_MinimapIcon.transform.localScale.y, scale);
            m_MinimapIcon.GetComponent<MeshRenderer>().material.color = GameUI.Instance.MinimapUnitColors[(int)m_Team];
        }

        #endregion


        #region Cleanup

        /// <summary>
        /// Removes the references to the given unit wherever they appear.
        /// </summary>
        /// <param name="unit">The <c>Unit</c> that should be removed.</param>
        public void RemoveRefrencesToUnit(Unit unit)
        {
            m_MovementHandler.StopFollowingUnit(unit);
            m_WideRangeDetector.RemoveObject(unit.gameObject);
            m_MidRangeDetector.RemoveTarget(unit.gameObject);
        }

        /// <summary>
        /// Removes the references to the given settlement wherever they appear.
        /// </summary>
        /// <param name="settlement">The <c>SETTLEMENT</c> that should be removed.</param>
        public void RemoveRefrencesToSettlement(Settlement settlement)
        {
            m_WideRangeDetector.RemoveObject(settlement.gameObject);
            m_MidRangeDetector.RemoveTarget(settlement.gameObject);
        }

        #endregion
    }
}