using System;
using UIForia.Parsing;
using UIForia.Parsing.Expressions;
using UIForia.Util;

namespace UIForia.Compilers {

    public struct SlotDefinition {

        public const byte k_UnassignedParent = 255;
        
        public string slotName;
        public short slotId;
        public SlotType slotType;
        
        public byte parentSlotId_0;
        public byte parentSlotId_1;
        public byte parentSlotId_2;
        public byte parentSlotId_3;
        public StructList<AttributeDefinition> contextAttributes;

        public SlotDefinition(string slotName, SlotType slotType) {
            this.slotName = slotName;
            this.slotId = -1;
            this.parentSlotId_0 = k_UnassignedParent;
            this.parentSlotId_1 = k_UnassignedParent;
            this.parentSlotId_2 = k_UnassignedParent;
            this.parentSlotId_3 = k_UnassignedParent;
            this.slotType = slotType;
            this.contextAttributes = null;
        }
        
        public int this[int i] {
            get {
                switch (i) {
                    case 0:
                        return parentSlotId_0;
                    case 1:
                        return parentSlotId_1;
                    case 2:
                        return parentSlotId_2;
                    case 3:
                        return parentSlotId_3;
                    default:
                        return -1;
                }
            }
            set {
                switch (i) {
                    case 0:
                        parentSlotId_0 = (byte)value;
                        break;
                    case 1:
                        parentSlotId_1 = (byte)value;
                        break;
                    case 2:
                        parentSlotId_2 = (byte)value;
                        break;
                    case 3:
                        parentSlotId_3 = (byte)value;
                        break;
                    default:
                        throw new ArgumentException();
                }
            }
        }

        public bool IsTemplate => slotType == SlotType.Template;

    }

}