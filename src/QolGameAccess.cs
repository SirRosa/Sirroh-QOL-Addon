using System;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace Sirroh.Qol
{
    /// <summary>
    /// Cached Harmony accessors for Valheim's internal chest and inventory GUI
    /// fields. No repeated reflection in hotkey operations or inventory drawing.
    /// </summary>
    internal static class QolGameAccess
    {
        // ExtraSlots stores special slots below the regular rows of the SAME Inventory.
        // This optional boundary is used by Sort and QuickStack only, never by Restock.
        private static Func<int> _extraSlotsRegularHeight;
        private static bool _extraSlotsLookupDone;

        internal static bool TryGetQuickSortHeight(Inventory inventory, out int regularHeight)
        {
            regularHeight = inventory == null ? 0 : inventory.GetHeight();
            if (inventory == null) return false;
            if (!Chainloader.PluginInfos.TryGetValue("shudnal.ExtraSlots", out PluginInfo plugin))
                return true;

            if (!_extraSlotsLookupDone && plugin.Instance != null)
            {
                _extraSlotsLookupDone = true;
                try
                {
                    Type api = plugin.Instance.GetType().Assembly.GetType("ExtraSlots.API");
                    MethodInfo method = api == null ? null : api.GetMethod(
                        "GetInventoryHeightPlayer", BindingFlags.Public | BindingFlags.Static);
                    if (method != null && method.ReturnType == typeof(int) &&
                        method.GetParameters().Length == 0)
                        _extraSlotsRegularHeight = (Func<int>)Delegate.CreateDelegate(
                            typeof(Func<int>), method);
                }
                catch (Exception) { /* Fail closed: do not expose protected slots. */ }
            }

            if (_extraSlotsRegularHeight == null) return false;
            try
            {
                int boundary = _extraSlotsRegularHeight();
                if (boundary < 1 || boundary > inventory.GetHeight()) return false;
                regularHeight = boundary;
                return true;
            }
            catch (Exception) { return false; }
        }

        private static readonly AccessTools.FieldRef<InventoryGui,ItemDrop.ItemData> GuiDragged =
            AccessTools.FieldRefAccess<InventoryGui,ItemDrop.ItemData>("m_dragItem");
        private static readonly AccessTools.FieldRef<InventoryGui,Inventory> GuiSource =
            AccessTools.FieldRefAccess<InventoryGui,Inventory>("m_dragInventory");
        private static readonly AccessTools.FieldRef<InventoryGui,int> GuiCount =
            AccessTools.FieldRefAccess<InventoryGui,int>("m_dragAmount");
        private static readonly AccessTools.FieldRef<InventoryGui,Container> GuiContainer =
            AccessTools.FieldRefAccess<InventoryGui,Container>("m_currentContainer");
        private static readonly AccessTools.FieldRef<InventoryGrid,Inventory> GridInventory =
            AccessTools.FieldRefAccess<InventoryGrid,Inventory>("m_inventory");
        private static readonly AccessTools.FieldRef<Container,Inventory> ChestInventory =
            AccessTools.FieldRefAccess<Container,Inventory>("m_inventory");
        private static readonly AccessTools.FieldRef<Container,ZNetView> ChestView =
            AccessTools.FieldRefAccess<Container,ZNetView>("m_nview");
        private static readonly AccessTools.FieldRef<Container,Piece> ChestPiece =
            AccessTools.FieldRefAccess<Container,Piece>("m_piece");
        private static readonly Func<InventoryGrid,GameObject,Vector2i> Position =
            AccessTools.MethodDelegate<Func<InventoryGrid,GameObject,Vector2i>>(
                AccessTools.Method(typeof(InventoryGrid),"GetButtonPos",new[]{typeof(GameObject)}));
        private static readonly Action<InventoryGui,ItemDrop.ItemData,Inventory,int> SetDrag =
            AccessTools.MethodDelegate<Action<InventoryGui,ItemDrop.ItemData,Inventory,int>>(
                AccessTools.Method(typeof(InventoryGui),"SetupDragItem",
                    new[]{typeof(ItemDrop.ItemData),typeof(Inventory),typeof(int)}));
        private static readonly Action<Inventory,bool,bool> ChangedInventory =
            AccessTools.MethodDelegate<Action<Inventory,bool,bool>>(
                AccessTools.Method(typeof(Inventory),"Changed",new[]{typeof(bool),typeof(bool)}));
        private static readonly Action<Container> SaveChest =
            AccessTools.MethodDelegate<Action<Container>>(AccessTools.Method(typeof(Container),"Save"));
        private static readonly Func<Container,long,bool> Access =
            AccessTools.MethodDelegate<Func<Container,long,bool>>(
                AccessTools.Method(typeof(Container),"CheckAccess",new[]{typeof(long)}));

        internal static ItemDrop.ItemData DragItem(InventoryGui gui) => GuiDragged(gui);
        internal static Inventory DragSource(InventoryGui gui) => GuiSource(gui);
        internal static int DragAmount(InventoryGui gui) => GuiCount(gui);
        internal static Container CurrentContainer(InventoryGui gui) => GuiContainer(gui);
        internal static Inventory Inventory(InventoryGrid grid) => GridInventory(grid);
        internal static Inventory Inventory(Container chest) => ChestInventory(chest);
        internal static ZNetView View(Container chest) => ChestView(chest);
        internal static Piece Piece(Container chest) => ChestPiece(chest);
        internal static Vector2i ButtonPosition(InventoryGrid grid,GameObject button) => Position(grid, button);
        internal static void ClearDrag(InventoryGui gui) => SetDrag(gui,null,null,0);
        internal static void Save(Container chest) => SaveChest(chest);
        internal static void NotifyChanged(Inventory inventory) => ChangedInventory(inventory,false,false);
        internal static bool CheckAccess(Container chest,long id) => Access(chest,id);
    }
}
