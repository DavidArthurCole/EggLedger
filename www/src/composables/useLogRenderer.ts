export type LogSegment =
  | { type: 'text'; text: string; color?: string }
  | { type: 'image'; src: string }
  | { type: 'eid-bar' }

// A matched token: the segments it produces and how many input chars it consumed.
type TokenMatch = { segments: LogSegment[], consumed: number }

// [eid-bar] token
function matchEidBar(s: string): TokenMatch | null {
  if (!s.startsWith('[eid-bar]')) return null
  return { segments: [{ type: 'eid-bar' }], consumed: '[eid-bar]'.length }
}

// [img:filename] token
function matchImage(s: string): TokenMatch | null {
  const m = /^\[img:([^\]]+)\]/.exec(s)
  if (!m) return null
  return { segments: [{ type: 'image', src: 'images/' + m[1] }], consumed: m[0].length }
}

// &rrggbb<text> color token (text may itself embed [eid-bar] separators)
function matchColor(s: string): TokenMatch | null {
  const m = /^&([0-9a-fA-F]{6})<([^>]*)>/.exec(s)
  if (!m) return null
  const color = '#' + m[1]
  const segments: LogSegment[] = []
  const parts = m[2].split('[eid-bar]')
  for (let i = 0; i < parts.length; i++) {
    if (parts[i]) segments.push({ type: 'text', text: parts[i], color })
    if (i < parts.length - 1) segments.push({ type: 'eid-bar' })
  }
  return { segments, consumed: m[0].length }
}

const tokenMatchers = [matchEidBar, matchImage, matchColor]

export function parseLogSegments(message: string): LogSegment[] {
  const segments: LogSegment[] = []
  let remaining = message

  while (remaining.length > 0) {
    let matched: TokenMatch | null = null
    for (const matcher of tokenMatchers) {
      matched = matcher(remaining)
      if (matched) break
    }
    if (matched) {
      segments.push(...matched.segments)
      remaining = remaining.slice(matched.consumed)
      continue
    }

    // Plain text: consume up to the next token or end of string
    const nextToken = remaining.search(/\[eid-bar\]|\[img:|&[0-9a-fA-F]{6}</)
    if (nextToken === -1) {
      segments.push({ type: 'text', text: remaining })
      break
    }
    const cutAt = nextToken === 0 ? 1 : nextToken
    segments.push({ type: 'text', text: remaining.slice(0, cutAt) })
    remaining = remaining.slice(cutAt)
  }

  return segments
}
