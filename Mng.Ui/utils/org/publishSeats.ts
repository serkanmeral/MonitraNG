export type PublishSeat = {
  id: string;
  ad: string;
  ust: string;
  departman: string;
  oturan: string;
  calisanlar: string[];
};

export type PublishCard = {
  id: string;
  keeperId: string;
};

export type OrgPublishUser = {
  userId: string;
  title: string | null;
  department: string;
};

export type OrgPublishPlan = {
  error?: string;
  users: OrgPublishUser[];
  managers: { dataId: string; yoneticiId: string }[];
  withoutCard: number;
};

function emptyPlan(error: string): OrgPublishPlan {
  return { error, users: [], managers: [], withoutCard: 0 };
}

export function planOrgPublish(seats: PublishSeat[], cards: PublishCard[]): OrgPublishPlan {
  const seen = new Set<string>();
  const placements: { userId: string; title: string | null; department: string; managerId: string }[] = [];

  for (const seat of seats) {
    const staff = (seat.calisanlar || []).map((id) => id.trim()).filter(Boolean);
    const occupant = seat.oturan.trim();
    if (occupant && staff.includes(occupant)) return emptyPlan('Oturan kişi çalışanlar arasında olamaz.');
    if (occupant && !seat.ad.trim()) return emptyPlan('Mevki adı boş kalmaz.');

    const people = [
      occupant ? { userId: occupant, title: seat.ad.trim(), managerId: '' } : null,
      ...staff.map((userId) => ({ userId, title: null as string | null, managerId: occupant })),
    ].filter((item): item is { userId: string; title: string | null; managerId: string } => !!item);

    for (const person of people) {
      if (seen.has(person.userId)) return emptyPlan('Bu kullanıcı başka bir mevkide oturuyor.');
      seen.add(person.userId);
      placements.push({
        userId: person.userId,
        title: person.title,
        department: seat.departman.trim(),
        managerId: person.managerId,
      });
    }
  }

  if (!placements.length) return emptyPlan('Yayınlanacak kişi yok.');

  const byId = new Map(seats.map((seat) => [seat.id, seat]));
  const cardsByKeeper = new Map<string, PublishCard[]>();
  for (const card of cards) {
    if (!card.keeperId || !card.id) continue;
    const list = cardsByKeeper.get(card.keeperId) || [];
    list.push(card);
    cardsByKeeper.set(card.keeperId, list);
  }

  const users = placements.map((item) => ({
    userId: item.userId,
    title: item.title,
    department: item.department,
  }));

  const managers: { dataId: string; yoneticiId: string }[] = [];
  let withoutCard = 0;
  for (const seat of seats) {
    const occupant = seat.oturan.trim();
    const staff = (seat.calisanlar || []).map((id) => id.trim()).filter(Boolean);
    const parent = seat.ust ? byId.get(seat.ust) : undefined;
    const assignments = [
      occupant ? { userId: occupant, yoneticiId: parent?.oturan || '' } : null,
      ...staff.map((userId) => ({ userId, yoneticiId: occupant })),
    ].filter((item): item is { userId: string; yoneticiId: string } => !!item);

    for (const assignment of assignments) {
      const matches = cardsByKeeper.get(assignment.userId) || [];
      if (!matches.length) {
        withoutCard += 1;
        continue;
      }
      for (const card of matches) managers.push({ dataId: card.id, yoneticiId: assignment.yoneticiId });
    }
  }

  return { users, managers, withoutCard };
}

export function genderToInt(value: unknown): number {
  if (value === 1 || value === '1' || value === 'Male') return 1;
  if (value === 2 || value === '2' || value === 'Female') return 2;
  return 0;
}

export function keeperProfileBody(
  card: Record<string, unknown>,
  title: string,
  department: string,
) {
  const text = (camel: string, pascal: string) => {
    const value = card[camel] !== undefined ? card[camel] : card[pascal];
    return value == null ? '' : String(value);
  };
  const nullable = (camel: string, pascal: string) => {
    const value = card[camel] !== undefined ? card[camel] : card[pascal];
    if (value == null || String(value).trim() === '') return null;
    return String(value);
  };
  const flag = (camel: string, pascal: string, fallback: boolean) => {
    const value = card[camel] !== undefined ? card[camel] : card[pascal];
    return typeof value === 'boolean' ? value : fallback;
  };

  return {
    username: text('username', 'Username'),
    email: text('email', 'Email'),
    firstName: text('firstName', 'FirstName'),
    lastName: text('lastName', 'LastName'),
    gender: genderToInt(card.gender !== undefined ? card.gender : card.Gender),
    phoneNumber: nullable('phoneNumber', 'PhoneNumber'),
    photoUrl: nullable('photoUrl', 'PhotoUrl'),
    isActive: flag('isActive', 'IsActive', true),
    includeInApplication: flag('includeInApplication', 'IncludeInApplication', true),
    title,
    department,
    ownProfileByOrg: true,
  };
}
