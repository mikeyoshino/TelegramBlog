export function filterPosts(posts, state = {}) {
 const {tab, group = 'all', topic = 'all', type = 'all', query = '', mediaOnly = false, sort = 'new', saved = new Set()} = state;
 const needle = query.trim().toLocaleLowerCase();
 return posts.filter(p =>
  (!mediaOnly || p.media.some(m => ['photo','image','video'].includes(m.kind))) &&
  (tab !== 'saved' || saved.has(p.id)) &&
  (tab === 'saved' || group === 'all' || p.group === group) &&
  (tab === 'saved' || topic === 'all' || p.topic === topic) &&
  (type === 'all' || (type === 'album' && p.media.length > 1) ||
   (type === 'photo' && p.media.some(m => (m.kind === 'image' || m.kind === 'photo'))) ||
   (type === 'video' && p.media.some(m => m.kind === 'video'))) &&
  `${p.title} ${p.text || ''} ${(p.links || []).join(' ')} ${p.tags || ''} ${p.topicTitle || ''} ${p.groupTitle || ''}`.toLocaleLowerCase().includes(needle)
 ).sort((a,b) => ((new Date(b.date) - new Date(a.date)) || (Number(String(b.id).split(':').at(-1))-Number(String(a.id).split(':').at(-1))) || 0) * (sort === 'old' ? -1 : 1));
}
export class RequestEpoch {
 #value = 0;
 next() { return ++this.#value; }
 current(value) { return value === this.#value; }
}
