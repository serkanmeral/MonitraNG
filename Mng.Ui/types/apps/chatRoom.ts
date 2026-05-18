/** DataGateway `cht_messages.roomKind` değerleri */
export type ChatRoomKind = 'direct' | 'topic' | 'group';

export interface ChatRoomSelection {
  roomKind: ChatRoomKind;
  roomRecordId: string;
  title: string;
  subtitle?: string;
}

export interface ChtMessageVm {
  dataId: string;
  roomKind: string;
  roomRecordId: string;
  body: string;
  authorPersonId: string;
  createdAt: string;
}

export interface ChtDirectConversationVm {
  dataId: string;
  participantAId: string;
  participantBId: string;
  lastMessageAt?: string | null;
}

export interface ChtTopicRoomVm {
  dataId: string;
  title: string;
  parentTopicRoomId?: string | null;
  archived?: boolean;
}

export interface ChtGroupChatVm {
  dataId: string;
  keycloakGroupId: string;
  displayNameCache?: string | null;
}
