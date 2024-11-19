using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Populous
{
    /// <summary>
    /// The <c>Unit</c> class is a <c>MonoBehavior</c> which represents and handles the functioning of one unit.
    /// Units are an abstraction of the population of the world, where one unit represents a group of people.
    /// </summary>
    public class Unit : NetworkBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private GameObject[] m_LeaderSigns;
        [SerializeField] private GameObject m_KnightSword;
        [SerializeField] private float m_SecondsUntilEnteringEnabled = 0.5f;

        [Header("Detectors")]
        [SerializeField] private UnitCloseRangeDetector m_CloseRangeDetector;
        [SerializeField] private UnitMidRangeDetector m_MidRangeDetector;
        [SerializeField] private UnitWideRangeDetector m_WideRangeDetector;
        [SerializeField] private int m_CloseRangeRadius = 15;
        [SerializeField] private int m_MidRangeTilesPerSide = 1;
        [SerializeField] private int m_WideRangeTilesPerSide = 20;

        private UnitMovementHandler m_MovementHandler;

        /// <summary>
        /// The <c>MapPoint</c> on the terrain grid which is closest to the current position of the unit.
        /// </summary>
        public MapPoint ClosestMapPoint { get => new(gameObject.transform.position.x, gameObject.transform.position.z); }

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


        /// <summary>
        /// Sets up the properties and different components of the unit.
        /// </summary>
        /// <param name="team">The <c>Team</c> the unit belongs to.</param>
        /// <param name="strength">The starting maxStrength of the unit.</param>
        public void Setup(Team team, int strength, bool canEnterSettlement)
        {
            m_Team = team;
            m_Strength = team == Team.RED ? 1 : 2/*strength*/;
            m_CanEnterSettlement = canEnterSettlement;

            m_MovementHandler = GetComponent<UnitMovementHandler>();
            m_MovementHandler.InitializeMovement();

            m_CloseRangeDetector.Setup(this);
            m_WideRangeDetector.Setup(team, m_WideRangeTilesPerSide);
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
                ToggleKnightSword(true);
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
            m_WideRangeDetector.StateChange(unitBehavior);
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
            UpdateUnitUI();
        }

        /// <summary>
        /// Removes the given amount of maxStrength from the unit.
        /// </summary>
        /// <param name="amount">The amount of maxStrength to be removed.</param>
        public void LoseStrength(int amount, bool isDamaged = true) 
        { 
            m_Strength -= amount;
            UpdateUnitUI();

            if (m_Strength == 0)
                UnitManager.Instance.DespawnUnit(gameObject, isDamaged);
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

            // TODO: ones on the edges shouldn't disappear if they haven't been sunk
            if (height <= Terrain.Instance.WaterLevel)
                UnitManager.Instance.DespawnUnit(gameObject);
            else
                SetHeight/*ClientRpc*/(height);
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
        public Vector3 GetUnitsDirection() => m_WideRangeDetector.GetAverageDirection();

        /// <summary>
        /// Gets a unit that this unit can follow.
        /// </summary>
        /// <returns>A <c>Unit</c> to be followed if one is found, null otherwise.</returns>
        public Unit GetFollowTarget() => m_MidRangeDetector.GetTarget();

        /// <summary>
        /// Stops this unit from following its target, if that target is the given <c>GameObject</c>.
        /// </summary>
        /// <param name="target">The <c>GameObject</c> that should be checked against the target.</param>
        public void LoseTarget(Unit target) => m_MovementHandler.StopFollowingUnit(target);

        /// <summary>
        /// Reacts to a new unit being assigned as the leader of the team, if this unit isn't the leader.
        /// </summary>
        public void NewLeaderUnitGained()
        {
            if (m_Class == UnitClass.LEADER || m_Behavior != UnitBehavior.GO_TO_SYMBOL) return;
            m_MovementHandler.GoToSymbol();
        }

        #endregion


        #region Team symbol

        /// <summary>
        /// Sets a new target for the unit movement if it is going to its faction symbol.
        /// </summary>
        public void SymbolLocationChanged()
        {
            if (m_Behavior != UnitBehavior.GO_TO_SYMBOL) return;
            m_MovementHandler.GoToSymbol();
        }

        /// <summary>
        /// Sets unit behavior for when it has reached its faction symbol.
        /// </summary>
        public void TeamSymbolReached()
        {
            if (m_Behavior != UnitBehavior.GO_TO_SYMBOL) return;
            m_MovementHandler.SymbolReached = true;
        }

        #endregion


        #region UI

        /// <summary>
        /// Called when the mouse cursor hovers over the unit.
        /// </summary>
        /// <param name="eventData">Event data for the pointer event.</param>
        public void OnPointerEnter(PointerEventData eventData) => ToggleUnitUIServer/*Rpc*/(true);

        /// <summary>
        /// Called when the mouse cursor stops hovering over the unit.
        /// </summary>
        /// <param name="eventData">Event data for the pointer event.</param>
        public void OnPointerExit(PointerEventData eventData) => ToggleUnitUIServer/*Rpc*/(false);


        /// <summary>
        /// Shows or hides the info for the unit on the UI.
        /// </summary>
        /// <param name="show">True if the UI should be active, false otherwise.</param>
        /// <param name="parameters">RPC data for the server RPC.</param>
        //[ServerRpc(RequireOwnership = false)]
        private void ToggleUnitUIServer/*Rpc*/(bool show, ServerRpcParams parameters = default)
        {
            if (IsInFight)
                UnitManager.Instance.ToggleFightUI(show, m_FightId, parameters.Receive.SenderClientId);
            else
            {
                ToggleUnitUIClient/*Rpc*/(show, UnitManager.Instance.MaxUnitStrength, m_Strength, GameController.Instance.TeamColors[(int)m_Team],
                    new ClientRpcParams()
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { parameters.Receive.SenderClientId }
                        }
                    }
                );
            }
        }

        /// <summary>
        /// Shows or hides the info for the unit on the UI.
        /// </summary>
        /// <param name="show">True if the UI should be active, false otherwise.</param>
        /// <param name="maxStrength">The maximum value of the strength bar.</param>
        /// <param name="currentStrength">The current value of the strength bar.</param>
        /// <param name="teamColor">The color of the strength bar.</param>
        /// <param name="parameters">RPC data for the client RPC.</param>
        //[ClientRpc]
        private void ToggleUnitUIClient/*Rpc*/(bool show, int maxStrength, int currentStrength, Color teamColor, ClientRpcParams parameters = default)
        {
            GameUI.Instance.ToggleUnitUI(
                show,
                maxStrength,
                currentStrength,
                teamColor,
                transform.position + Vector3.up * GetComponent<Renderer>().bounds.size.y
            );
        }

        /// <summary>
        /// Updates the unit info on the UI.
        /// </summary>
        private void UpdateUnitUI()
        {
            if (IsInFight)
                UnitManager.Instance.UpdateFightUI(m_FightId);
            else
                UpdateUnitUIClient/*Rpc*/(UnitManager.Instance.MaxUnitStrength, m_Strength);
        }

        /// <summary>
        /// Updates the unit info on the UI.
        /// </summary>
        /// <param name="maxStrength">The maximum value of the strength bar.</param>
        /// <param name="currentStrength">The current value of the strength bar.</param>
        /// <param name="parameters">RPC data for the client RPC.</param>
        //[ClientRpc]
        private void UpdateUnitUIClient/*Rpc*/(int maxStrength, int currentStrength, ClientRpcParams parameters = default)
            => GameUI.Instance.UpdateUnitUI(maxStrength, currentStrength);

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
            m_MidRangeDetector.RemoveTarget(unit);
        }

        /// <summary>
        /// Removes the references to the given settlement wherever they appear.
        /// </summary>
        /// <param name="settlement">The <c>Settlement</c> that should be removed.</param>
        public void RemoveRefrencesToSettlement(Settlement settlement)
            => m_WideRangeDetector.RemoveObject(settlement.gameObject);

        #endregion
    }
}