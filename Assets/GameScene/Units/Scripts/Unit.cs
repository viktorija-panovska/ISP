using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Populous
{
    /// <summary>
    /// The <c>Unit</c> class represents and handles the functioning of one unit.
    /// </summary>
    public class Unit : NetworkBehaviour, IInspectableObject, ILeader
    {
        #region Inspector Fields

        [Tooltip("The amount of seconds between a unit being spawned and it being able to enter settlements.")]
        [SerializeField] private float m_SecondsUntilEnteringEnabled = 0.5f;
        [Tooltip("The GameObjects of the signs carried by a Leader unit, where index 0 is the red leader sign and index 1 is the blue leader sign.")]
        [SerializeField] private GameObject[] m_LeaderSigns;
        [Tooltip("The GameObject of the sword carried by a Knight unit.")]
        [SerializeField] private GameObject m_KnightSword;
        [Tooltip("The GameObject of the highlight enabled when the unit is clicked in Query mode.")]
        [SerializeField] private GameObject m_Highlight;
        [Tooltip("The GameObject of the icon for the unit on the minimap.")]
        [SerializeField] private GameObject m_MinimapIcon;

        [Header("Detectors")]
        [SerializeField] private UnitCollisionDetector m_CollisionDetector;
        [SerializeField] private UnitChaseDetector m_ChaseDetector;
        [SerializeField] private UnitDirectionDetector m_DirectionDetector;

        #endregion


        #region Class Fields

        /// <summary>
        /// The GameObject associated with the unit.
        /// </summary>
        public GameObject GameObject { get => gameObject; }

        private Faction m_Faction;
        /// <summary>
        /// Gets the faction this unit belongs to.
        /// </summary>
        public Faction Faction { get => m_Faction; }

        private UnitType m_Type;
        /// <summary>
        /// Gets the type of this unit.
        /// </summary>
        public UnitType Type { get => m_Type; }

        private UnitBehavior m_Behavior;
        /// <summary>
        /// Gets the current behavior of this unit.
        /// </summary>
        public UnitBehavior Behavior { get => m_Behavior; }

        private int m_Strength;
        /// <summary>
        /// The current strength of the unit.
        /// </summary>
        /// <remarks>The strength of the unit is the number of followers represented by this one unit.</remarks>
        public int Strength { get => m_Strength; }

        private bool m_CanEnterSettlement;
        /// <summary>
        /// True if the followers from the unit can enter a settlement, false otherwise.
        /// </summary>
        public bool CanEnterSettlement { get => m_CanEnterSettlement; }


        /// <summary>
        /// A reference to the unit's movement handler.
        /// </summary>
        private UnitMovementHandler m_MovementHandler;

        /// <summary>
        /// The <c>TerrainPoint</c> on the terrain grid which is closest to the current position of the unit.
        /// </summary>
        public TerrainPoint ClosestTerrainPoint { get => new(gameObject.transform.position); }

        /// <summary>
        /// The tile the unit is currently on.
        /// </summary>
        public TerrainTile CurrentTile { get => new(gameObject.transform.position.x, gameObject.transform.position.z); }


        private bool m_IsInFight;
        /// <summary>
        /// True if the unit is currently in a fight with a unit of the opposite team, false otherwise.
        /// </summary>
        public bool IsInFight { get => m_IsInFight; }

        private int m_FightId = -1;
        /// <summary>
        /// Gets the idenitifier of the fight the unit is involved in.
        /// </summary>
        /// <remarks>-1 if the unit is not involved in a fight.</remarks>
        public int FightId { get => m_FightId; }

        private readonly NetworkVariable<bool> m_IsInspected = new();
        /// <summary>
        /// True if the unit is being inspected by any player, false otherwise.
        /// </summary>
        public bool IsInspected { get => m_IsInspected.Value; set => m_IsInspected.Value = value; }

        #endregion


        #region Setup

        /// <summary>
        /// Initializes the unit.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> the structure belongs to.</param>
        /// <param name="strength">The starting strength of the unit.</param>
        /// <param name="canEnterSettlement">True if the unit can enter a settlement immediately after spawning, false if it has to wait a bit.</param>
        public void Setup(Faction faction, int strength, bool canEnterSettlement)
        {
            m_Faction = faction;
            m_Strength = strength;
            m_CanEnterSettlement = canEnterSettlement;

            SetObjectInfo_ClientRpc(
                $"{m_Faction} Unit",
                GameController.Instance.FactionColors[(int)m_Faction],
                LayerData.FactionLayers[(int)m_Faction]
            );

            m_CollisionDetector.Setup(this);
            m_ChaseDetector.Setup(this);
            m_DirectionDetector.Setup(this);

            m_MovementHandler = GetComponent<UnitMovementHandler>();
            m_MovementHandler.InitializeMovement();

            SetupMinimapIcon();

            // subscribe to events
            if (m_Faction == Faction.RED)
            {
                UnitManager.Instance.OnRedBehaviorChange += SetBehavior;
                UnitManager.Instance.OnNewRedLeaderGained += FollowLeader;
                GameController.Instance.OnRedMagnetMoved += UpdateUnitMagnetTargetLocation;
            }
            else if (m_Faction == Faction.BLUE)
            {
                UnitManager.Instance.OnBlueBehaviorChange += SetBehavior;
                UnitManager.Instance.OnNewBlueLeaderGained += FollowLeader;
                GameController.Instance.OnBlueMagnetMoved += UpdateUnitMagnetTargetLocation;
            }

            GameController.Instance.OnTerrainModified += UpdateHeight;
            GameController.Instance.OnTerrainModified += m_MovementHandler.CheckTargetTile;
            GameController.Instance.OnFlood += UpdateHeight;
            GameController.Instance.OnFlood += m_MovementHandler.CheckTargetTile;
            UnitManager.Instance.OnRemoveReferencesToUnit += RemoveRefrencesToUnit;
            StructureManager.Instance.OnRemoveReferencesToSettlement += RemoveRefrencesToSettlement;

            if (!m_CanEnterSettlement)
                StartCoroutine(WaitToEnableEntering());
        }

        /// <summary>
        /// Sets up some <c>GameObject</c> properties for the given unit.
        /// </summary>
        /// <param name="unitNetworkId">The <c>NetworkObjectId</c> of the unit.</param>
        /// <param name="name">The name for the <c>GameObject</c> of the unit.</param>
        /// <param name="color">The color the body of the unit should be set to.</param>
        /// <param name="layer">An integer representing the layer the unit should be on.</param>
        [ClientRpc]
        private void SetObjectInfo_ClientRpc(string name, Color color, int layer)
        {
            gameObject.name = name;
            gameObject.layer = layer;
            gameObject.GetComponent<MeshRenderer>().material.color = color;
        }

        /// <summary>
        /// Sets up the icon that represents the unit on the minimap.
        /// </summary>
        private void SetupMinimapIcon()
        {
            float scale = UnitManager.Instance.MinimapIconScale;

            m_MinimapIcon.transform.localScale = new(scale, m_MinimapIcon.transform.localScale.y, scale);
            m_MinimapIcon.GetComponent<MeshRenderer>().material.color = UnitManager.Instance.MinimapUnitColors[(int)m_Faction];
        }

        #endregion


        #region Cleanup

        /// <summary>
        /// Cleans up references to other objects before the destruction of the unit.
        /// </summary>
        public void Cleanup()
        {
            // unsubscribe from events
            if (m_Faction == Faction.RED)
            {
                UnitManager.Instance.OnRedBehaviorChange -= SetBehavior;
                UnitManager.Instance.OnNewRedLeaderGained -= FollowLeader;
                GameController.Instance.OnRedMagnetMoved -= UpdateUnitMagnetTargetLocation;
            }
            else if (m_Faction == Faction.BLUE)
            {
                UnitManager.Instance.OnBlueBehaviorChange -= SetBehavior;
                UnitManager.Instance.OnNewBlueLeaderGained -= FollowLeader;
                GameController.Instance.OnBlueMagnetMoved -= UpdateUnitMagnetTargetLocation;
            }

            GameController.Instance.OnTerrainModified -= UpdateHeight;
            GameController.Instance.OnTerrainModified -= m_MovementHandler.CheckTargetTile;
            GameController.Instance.OnFlood -= UpdateHeight;
            GameController.Instance.OnFlood -=  m_MovementHandler.CheckTargetTile;
            UnitManager.Instance.OnRemoveReferencesToUnit -= RemoveRefrencesToUnit;
            StructureManager.Instance.OnRemoveReferencesToSettlement -= RemoveRefrencesToSettlement;

            if (IsInspected)
                GameController.Instance.RemoveInspectedObject(this);

            //GameController.Instance.RemoveVisibleObject_ClientRpc(
            //    GetComponent<NetworkObject>().NetworkObjectId,
            //    GameUtils.GetClientParams(GameData.Instance.GetNetworkIdByTeam(m_Faction))
            //);
        }

        /// <summary>
        /// Removes the references to the given unit wherever they appear.
        /// </summary>
        /// <param name="unit">The <c>Unit</c> that should be removed.</param>
        public void RemoveRefrencesToUnit(Unit unit)
        {
            m_MovementHandler.StopFollowingUnit(unit);
            m_DirectionDetector.RemoveObject(unit.gameObject);
            m_ChaseDetector.RemoveTarget(unit.gameObject);
        }

        /// <summary>
        /// Removes the references to the given settlement wherever they appear.
        /// </summary>
        /// <param name="settlement">The <c>Settlement</c> that should be removed.</param>
        public void RemoveRefrencesToSettlement(Settlement settlement)
        {
            m_DirectionDetector.RemoveObject(settlement.gameObject);
            m_ChaseDetector.RemoveTarget(settlement.gameObject);
        }

        #endregion


        #region Behavior and Type

        /// <summary>
        /// Sets the current type of the unit to the given type.
        /// </summary>
        /// <param name="unitType">The <c>UnitType</c> that should be set.</param>
        public void SetType(UnitType unitType)
        {
            if (m_Type == unitType) return;

            if (m_Type == UnitType.LEADER)
                ToggleLeaderSign_ClientRpc(m_Faction, false);

            if (m_Type == UnitType.KNIGHT)
                ToggleKnightSword_ClientRpc(false);

            m_Type = unitType;

            if (m_Type == UnitType.LEADER)
                ToggleLeaderSign_ClientRpc(m_Faction, true);

            if (m_Type == UnitType.KNIGHT)
            {
                ToggleKnightSword_ClientRpc(true);
                SetBehavior(UnitBehavior.FIGHT);
                m_DirectionDetector.SetDetectorSize(Terrain.Instance.TilesPerSide);
            }

            if (IsInspected)
                GameController.Instance.UpdateInspectedUnit(this, updateType: true);
        }

        /// <summary>
        /// Sets or unsets this unit as the leader, based on the given value.
        /// </summary>
        /// <param name="isLeader">True if the unit should be set as the leader, false otherwise.</param>
        public void SetLeader(bool isLeader) => SetType(isLeader ? UnitType.LEADER : UnitType.WALKER);

        /// <summary>
        /// Sets the current behavior of the unit to the given behavior.
        /// </summary>
        /// <param name="unitBehavior">The <c>UnitBehavior</c> that should be set.</param>
        public void SetBehavior(UnitBehavior unitBehavior)
        {
            if (m_Behavior == unitBehavior) return;

            m_Behavior = unitBehavior;

            m_ChaseDetector.UpdateDetector();
            m_DirectionDetector.UpdateDetector();
        }

        /// <summary>
        /// Activates or deactivates the sign the leader should be holding.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose leader sign should be activated.</param>
        /// <param name="isOn">True if the sign should be activated, false otherwise.</param>
        [ClientRpc]
        private void ToggleLeaderSign_ClientRpc(Faction faction, bool isOn)
        {
            //m_LeaderSigns[(int)faction].SetActive(isOn);

            GameObject flag = GameUtils.GetChildWithTag(gameObject, TagData.LeaderTags[(int)faction]);
            Debug.Log(flag);

            if (!flag) return;

            flag.SetActive(isOn);
        }

        /// <summary>
        /// Activates or deactivates the sword a knight should be holding.
        /// </summary>
        /// <param name="isOn">True if the sword should be activated, false otherwise.</param>
        [ClientRpc]
        private void ToggleKnightSword_ClientRpc(bool isOn)
        {
            //m_KnightSword.SetActive(isOn);

            GameObject sword = GameUtils.GetChildWithTag(gameObject, TagData.SWORD_TAG);
            if (!sword) return;
            sword.SetActive(isOn);
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
        /// Adds the given amount of strength to the unit.
        /// </summary>
        /// <param name="amount">The amount of strength to be added.</param>
        public void GainStrength(int amount)
        {
            m_Strength += amount;

            if (IsInspected)
                GameController.Instance.UpdateInspectedUnit(this, updateStrength: true);
        }

        /// <summary>
        /// Removes the given amount of strength from the unit.
        /// </summary>
        /// <param name="amount">The amount of strength to be removed.</param>
        public void LoseStrength(int amount, bool isDamaged = true) 
        { 
            m_Strength -= amount;

            if (IsInspected && !IsInFight)
                GameController.Instance.UpdateInspectedUnit(this, updateStrength: true);

            if (m_Strength == 0)
                UnitManager.Instance.DespawnUnit(gameObject, hasDied: isDamaged);
        }

        #endregion


        #region Height

        /// <summary>
        /// Updates the height of the unit depending on the terrain under it.
        /// </summary>
        private void UpdateHeight() => UpdateHeight(new(0, 0), new(Terrain.Instance.TilesPerSide, Terrain.Instance.TilesPerSide));

        /// <summary>
        /// Updates the height of the unit depending on the terrain under it, if it is in the modified area.
        /// </summary>
        /// <param name="bottomLeft">The bottom-left corner of a rectangular area containing all modified terrain points.</param>
        /// <param name="topRight">The top-right corner of a rectangular area containing all modified terrain points.</param>
        private void UpdateHeight(TerrainPoint bottomLeft, TerrainPoint topRight)
        {
            Vector3 bottomLeftPosition = bottomLeft.ToScenePosition();
            Vector3 topRightPosition = topRight.ToScenePosition();

            m_MovementHandler.UpdateTargetPointHeight(bottomLeftPosition, topRightPosition);

            // only update points in this area
            if (transform.position.x < bottomLeftPosition.x || transform.position.x > topRightPosition.x ||
                transform.position.z < bottomLeftPosition.z || transform.position.z > topRightPosition.z)
                return;

            float height;

            Vector3 startPosition = m_MovementHandler.StartLocation.ToScenePosition();
            Vector3 endPosition = m_MovementHandler.EndLocation.ToScenePosition();

            if (startPosition.y == endPosition.y)
                height = startPosition.y;
            else
            {
                float heightDifference = Mathf.Abs(endPosition.y - startPosition.y);
                float totalDistance = new Vector2(endPosition.x - startPosition.x, endPosition.z - startPosition.z).magnitude;

                float distance = startPosition.y < endPosition.y
                    ? new Vector2(transform.position.x - startPosition.x, transform.position.z - startPosition.z).magnitude
                    : new Vector2(endPosition.x - transform.position.x, endPosition.z - transform.position.z).magnitude;

                height = heightDifference * distance / totalDistance;
                height = startPosition.y < endPosition.y ? startPosition.y + height : endPosition.y + height;
            }

            if (CurrentTile.IsUnderwater())
                UnitManager.Instance.DespawnUnit(gameObject, hasDied: true);
            else
                SetHeight_ClientRpc(height);
        }

        /// <summary>
        /// Sets the height that the unit stands at.
        /// </summary>
        /// <param name="height">The height the unit should stand at.</param>
        [ClientRpc]
        private void SetHeight_ClientRpc(float height) 
            => transform.position = new Vector3(transform.position.x, height, transform.position.z);

        #endregion


        #region Movement

        /// <summary>
        /// Starts or stops the movement of the unit.
        /// </summary>
        /// <param name="pause">True if the movement should be stopped, false otherwise.</param>
        public void ToggleMovement(bool pause) => m_MovementHandler.Pause(pause);

        /// <summary>
        /// Rotates the unit to face in the given direction.
        /// </summary>
        /// <param name="lookPosition">The direction the unit should be turned towards.</param>
        //[ClientRpc]
        public void Rotate/*_ClientRpc*/(Vector3 lookPosition)
        {
            if (lookPosition == Vector3.zero) return;
            transform.rotation = Quaternion.LookRotation(lookPosition);
        }


        #region Special Movement

        /// <summary>
        /// Gets a direction that this unit should move in if it wants to run across units and settlements it has detected in its direction detector.
        /// </summary>
        /// <returns>A <c>Vector3</c> representing the direction, zero vector if no units or settlements are detected in the vicinity.</returns>
        public Vector3 GetDetectedDirection() => m_DirectionDetector.GetAverageDirection();

        /// <summary>
        /// Gets a unit detected in this unit's chase detector.
        /// </summary>
        /// <returns>A <c>Unit</c> to be chased if one is found, null otherwise.</returns>
        public Unit GetUnitInChaseRange()
        {
            GameObject target = m_ChaseDetector.GetTarget();
            if (!target) return null;
            return target.GetComponent<Unit>();
        }

        /// <summary>
        /// Gets a settlement detected in this unit's chase detector.
        /// </summary>
        /// <returns>A <c>Settlement</c> to go to if one is found, null otherwise.</returns>
        public Settlement GetSettlementInChaseRange()
        {
            GameObject target = m_ChaseDetector.GetTarget();
            if (!target) return null;
            return target.GetComponent<Settlement>();
        }

        /// <summary>
        /// Stops this unit from chasing its target, if that target is the given <c>GameObject</c>.
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

        /// <summary>
        /// Has the unit follow the faction leader, if the unit is not the leader and it is going to the unit magnet.
        /// </summary>
        public void FollowLeader()
        {
            if (m_Type == UnitType.LEADER || m_Behavior != UnitBehavior.GO_TO_MAGNET) return;
            m_MovementHandler.FollowLeader();
        }

        #endregion


        #region Unit Magnet

        /// <summary>
        /// Sets a new target for the unit's movement, if the unit is going to the unit magnet.
        /// </summary>
        public void UpdateUnitMagnetTargetLocation()
        {
            if (m_Behavior != UnitBehavior.GO_TO_MAGNET) return;
            m_MovementHandler.GoToUnitMagnet();
        }

        /// <summary>
        /// Sets the "is unit magnet reached" flag to true.
        /// </summary>
        public void SetUnitMagnetReached()
        {
            if (m_Behavior != UnitBehavior.GO_TO_MAGNET) return;
            m_MovementHandler.IsUnitMagnetReached = true;
        }

        #endregion


        #endregion


        #region Fight

        /// <summary>
        /// Enters the unit into a fight.
        /// </summary>
        public void StartFight(int fightId)
        {
            m_MovementHandler.Pause(true);
            m_IsInFight = true;
            m_FightId = fightId;
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


        #region Inspecting

        /// <summary>
        /// Called when the mouse cursor hovers over the unit.
        /// </summary>
        /// <remarks>Called on client.</remarks>
        /// <param name="eventData">Event data for the pointer event.</param>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!PlayerController.Instance.IsQueryModeActive) return;
            SetHighlight(true);
        }

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
        /// Called when the mouse cursor clicks on the unit.
        /// </summary>
        /// <remarks>Called on client.</remarks>
        /// <param name="eventData">Event data for the pointer event.</param>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!PlayerController.Instance.IsQueryModeActive) return;
            GameController.Instance.SetInspectedObject_ServerRpc(PlayerController.Instance.Faction, GetComponent<NetworkObject>());
        }

        /// <summary>
        /// Activates or deactivates the highlight of the unit.
        /// </summary>
        /// <param name="shouldActivate">True if the highlight should be activated, false otherwise.</param>
        public void SetHighlight(bool shouldActivate) => m_Highlight.SetActive(shouldActivate);

        #endregion
    }
}