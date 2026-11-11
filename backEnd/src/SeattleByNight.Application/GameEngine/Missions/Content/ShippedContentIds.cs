namespace SeattleByNight.Application.GameEngine.Missions.Content;

// WF-27: the ids of the records the shipped content bundle authors, for the
// development seeder and the tests that refer to known content. The records
// themselves live in the bundle; these are only their (fixed) GUIDs.
public static class ShippedContentIds
{
    // The warehouse job site's four Private map rooms, seeded by the
    // development seeder.
    public static class SiteRooms
    {
        public static readonly Guid LoadingDock = new("e0e0e0e0-0000-4000-8000-000000000001");
        public static readonly Guid WarehouseFloor = new("e0e0e0e0-0000-4000-8000-000000000002");
        public static readonly Guid StorageRoom = new("e0e0e0e0-0000-4000-8000-000000000003");
        public static readonly Guid BackHallway = new("e0e0e0e0-0000-4000-8000-000000000004");
    }

    public static class NpcTemplates
    {
        public static readonly Guid MrJohnson = new("4cf1c180-6e71-41ca-8c1b-d33b50f3d06f");
        public static readonly Guid StreetGanger = new("c9598b8d-a483-4741-8463-29bf6e7b64f0");
    }

    public static class Tests
    {
        public static readonly Guid BlockAttacker = new("5ebec4f2-5d44-47cd-9167-13650586c518");
        public static readonly Guid DodgeGunfire = new("925745d3-f12b-41f6-8c5f-a38a5e6f265b");
        public static readonly Guid ReadShippingRecords = new("017e2a55-843f-4e5e-a7f9-102310427c33");
    }

    public static class Encounters
    {
        public static readonly Guid GangWarehouse = new("dd942386-7883-4d93-8201-571fdffb872c");
    }

    public static class Missions
    {
        public static readonly Guid GangWarehouseRetrieval = new("c5c1844a-eb16-4278-a8ce-89b90b3ff11d");
    }

    public static class Objectives
    {
        public static readonly Guid DeliverPackage = new("7b1f1d57-da5d-43ad-b46e-6f14a3a3b087");
        public static readonly Guid EnterWarehouse = new("23b71c4f-274e-4e16-b0f0-de7aa985ed57");
        public static readonly Guid LeaveWarehouse = new("47eb6a5c-7a96-4580-8a55-be595afc2988");
        public static readonly Guid RetrievePackage = new("0609ca43-dd89-440a-8f6b-19583d8ae55c");
    }

    public static class Triggers
    {
        public static readonly Guid AdvanceCleared = new("32398281-92f8-4508-bbba-0e29cca5a2e3");
        public static readonly Guid HallwayAmbush = new("9a136fe9-a6c3-4356-b4a4-d3ae6488fb77");
        public static readonly Guid LedgerTerminalRecords = new("45a97f57-2124-40bc-8122-6f0172c1ff4a");
    }

    public static class SiteNpcs
    {
        public static readonly Guid HallwayEnforcer = new("b4d53dea-e019-4c09-973c-28c99db4d077");
        public static readonly Guid WarehouseGanger = new("f83134f1-47e5-4cde-9fde-8277384646eb");
    }

    public static class Items
    {
        public static readonly Guid EnforcerKeycard = new("b6745f2f-2577-4e26-96d2-2e1b29b660eb");
        public static readonly Guid Package = new("86c291a8-4ad0-436c-ad63-73973a3b7c19");
    }

    public static class Inspectables
    {
        public static readonly Guid LedgerTerminal = new("575e22bf-3c6b-4921-ac9a-11bf80242872");
    }

    public static class Scenes
    {
        public static readonly Guid GangerLookoutTalk = new("ade067b9-e588-4d1f-99c3-2e5771095d9b");
        public static readonly Guid JohnsonWarehouseJob = new("213f1243-ea07-4d5b-b7b2-81d7be1258c6");
        public static readonly Guid WarehouseHallwayAmbush = new("2b40be93-5470-464d-802c-aedfa02171f2");
    }

    public static class SceneNodes
    {
        public static readonly Guid Accepted = new("97980703-bad5-46c5-8af5-a9ccd7a67a13");
        public static readonly Guid Ambush = new("5ca3b9a9-a05d-42fa-9d56-1bab4d656e84");
        public static readonly Guid Blocked = new("a1ad971d-7b2c-4d96-baf6-35db93b417d8");
        public static readonly Guid Confront = new("06ced137-db65-40e1-ae3b-cdcb28fefb70");
        public static readonly Guid Details = new("3354c0f5-ea5f-4bc2-986f-fe357f929856");
        public static readonly Guid Dodged = new("fb849827-97f4-4fac-bceb-bec317191db3");
        public static readonly Guid Greeting = new("ff06f881-e223-4df5-99e7-9b58f556c766");
        public static readonly Guid Hit = new("bd061242-984b-477c-9dee-6ece0f944aa0");
        public static readonly Guid InProgress = new("e1991fc6-733d-4036-be0a-6f25a9c909d0");
        public static readonly Guid JobOffer = new("8407b0c9-18cd-4e55-9ccc-3ef6aebfb836");
        public static readonly Guid Made = new("4e4029e3-f993-47c3-bb1f-fc55f36428c8");
        public static readonly Guid NegotiateLose = new("35a92c9d-d59c-4319-a201-ade435534bd2");
        public static readonly Guid NegotiateWin = new("a4ca1564-6cfb-41a6-825b-22e3d34eef49");
        public static readonly Guid Paid = new("4b1fa6da-ec2d-4da3-8850-1348744f1371");
        public static readonly Guid TalkedPast = new("8350a9f2-753a-4a96-9da3-63eaa2aa6337");
    }

    public static class SceneChoices
    {
        public static readonly Guid GangerLookoutTalkConfrontBackOff = new("4c08060a-8e2d-4d35-babf-0fe34cad48b1");
        public static readonly Guid GangerLookoutTalkConfrontTalkPast = new("14021948-2d29-4048-bf05-57dcbcb6948a");
        public static readonly Guid GangerLookoutTalkMadeLeave = new("cc6df3d0-da8a-4a66-b4b9-da51734d03fa");
        public static readonly Guid GangerLookoutTalkTalkedPastLeave = new("a201bb25-cffa-4d41-9698-344e01094cbf");
        public static readonly Guid JohnsonWarehouseJobAcceptedLeave = new("c42f4053-6085-4548-9887-e3d9248f1614");
        public static readonly Guid JohnsonWarehouseJobDetailsBackToOffer = new("d20226e0-0c90-47fb-80a3-fb86c4075adf");
        public static readonly Guid JohnsonWarehouseJobGreetingAskJob = new("a0dfd65a-0f4c-482c-b061-c886b1ce46a7");
        public static readonly Guid JohnsonWarehouseJobGreetingHandOverPackage = new("37fb88fb-be9a-4a40-b2e8-a32aa4251a6b");
        public static readonly Guid JohnsonWarehouseJobGreetingJobStatus = new("42ac6ae2-a937-4742-81c3-b543bd02867b");
        public static readonly Guid JohnsonWarehouseJobGreetingLeave = new("de81d040-b9de-4c68-85cc-f489c4512885");
        public static readonly Guid JohnsonWarehouseJobInProgressLeave = new("b7656906-3e1a-4d88-9eb8-99d6634e83c3");
        public static readonly Guid JohnsonWarehouseJobJobOfferAccept = new("36c37a4c-e45f-414c-9ecc-665ad4ba1c3b");
        public static readonly Guid JohnsonWarehouseJobJobOfferAskDetails = new("77fb6c62-cbcd-4c03-94d5-217a54a2d8db");
        public static readonly Guid JohnsonWarehouseJobJobOfferDecline = new("72809f7a-211e-4df1-812e-a99af2ceb80e");
        public static readonly Guid JohnsonWarehouseJobJobOfferNegotiate = new("587e6cbb-8f3a-4e06-b3d7-a54f35602a59");
        public static readonly Guid JohnsonWarehouseJobNegotiateLoseBackToOffer = new("6d850888-0de2-4524-bed8-1e3e3138a3be");
        public static readonly Guid JohnsonWarehouseJobNegotiateWinBackToOffer = new("6301d96e-e21a-4c6a-ad82-dc9f92c526a7");
        public static readonly Guid JohnsonWarehouseJobPaidLeave = new("db09d7f0-87c3-40a9-83e3-fef2a56bca70");
        public static readonly Guid WarehouseHallwayAmbushAmbushBlock = new("00fc98ad-0a10-4720-ac66-fb64e02d2ad0");
        public static readonly Guid WarehouseHallwayAmbushAmbushDodge = new("bc9f5043-31e0-4eb1-8b2e-c0d41efcc4a0");
        public static readonly Guid WarehouseHallwayAmbushBlockedTakeTheCard = new("08dda6d3-f84f-4b0b-beda-d803a0bb73d2");
        public static readonly Guid WarehouseHallwayAmbushDodgedComeUpSwinging = new("53ff2664-30a6-4f40-a304-7b04495a8f57");
        public static readonly Guid WarehouseHallwayAmbushHitFight = new("8ab53bee-b553-47db-abbb-5828bdcc9a90");
    }
}
