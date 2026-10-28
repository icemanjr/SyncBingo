import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AppRoutes } from '../App'
import { media } from '../test/setup'
import { adsTxt } from './adsTxt'

const client = 'ca-pub-1234567890123456'

function renderAt(path: string) {
  render(
    <MemoryRouter initialEntries={[path]}>
      <AppRoutes />
    </MemoryRouter>,
  )
}

const slots = () => screen.getAllByRole('complementary', { name: 'Advertisement' })
const units = () => Array.from(document.querySelectorAll<HTMLElement>('ins.adsbygoogle'))
const adScripts = () => document.querySelectorAll('script[src*="googlesyndication"]')

function useAdSense() {
  vi.stubEnv('VITE_AD_PROVIDER', 'adsense')
  vi.stubEnv('VITE_ADSENSE_CLIENT', client)
  vi.stubEnv('VITE_ADSENSE_SLOT_BANNER', '1111111111')
  vi.stubEnv('VITE_ADSENSE_SLOT_TOWER', '2222222222')
}

beforeEach(() => {
  window.adsbygoogle = []
})

afterEach(() => {
  vi.unstubAllEnvs()
  document.getElementById('adsense')?.remove()
  delete window.adsbygoogle
  delete window.googlefc
})

describe('ad slots (FR-50, FR-52, ADR 0012)', () => {
  it('show placeholders unless the build turns AdSense on, and load nothing from Google', () => {
    renderAt('/')

    expect(slots().every((slot) => slot.textContent === 'Ad')).toBe(true)
    expect(units()).toHaveLength(0)
    expect(adScripts()).toHaveLength(0)
    expect(screen.queryByRole('button', { name: 'Privacy and cookie settings' })).toBeNull()
  })

  it('stay placeholders when the provider is on but an ad unit is missing', () => {
    useAdSense()
    vi.stubEnv('VITE_ADSENSE_SLOT_TOWER', '')
    renderAt('/')

    expect(units()).toHaveLength(0)
    expect(adScripts()).toHaveLength(0)
  })

  it('on a phone, ask AdSense for the top and bottom banners, tagged for child treatment', () => {
    useAdSense()
    renderAt('/')

    const shown = units()
    expect(shown.map((ins) => ins.closest('aside')?.dataset.adSlot)).toEqual(['top', 'bottom'])
    for (const ins of shown) {
      expect(ins.dataset.adClient).toBe(client)
      expect(ins.dataset.adSlot).toBe('1111111111')
      expect(ins.dataset.tagForAgeTreatment).toBe('1')
      expect(ins.style.width).toBe('320px')
      expect(ins.style.height).toBe('50px')
    }
    expect(window.adsbygoogle).toHaveLength(2)
    expect(adScripts()).toHaveLength(1)
    expect(adScripts()[0]).toHaveAttribute(
      'src',
      `https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js?client=${client}`,
    )
  })

  it('on a desktop, ask for the side banners instead', () => {
    useAdSense()
    media.wide = true
    renderAt('/')

    const shown = units()
    expect(shown.map((ins) => ins.closest('aside')?.dataset.adSlot)).toEqual(['left', 'right'])
    expect(shown.every((ins) => ins.dataset.adSlot === '2222222222')).toBe(true)
    expect(shown.every((ins) => ins.dataset.tagForAgeTreatment === '1')).toBe(true)
  })

  it('keep their reserved size whatever they hold, so the page never shifts', () => {
    useAdSense()
    renderAt('/')

    const sizes = slots().map((slot) => [slot.dataset.adSlot, slot.style.width, slot.style.height])
    expect(sizes).toEqual([
      ['top', '320px', '50px'],
      ['left', '160px', '600px'],
      ['right', '160px', '600px'],
      ['bottom', '320px', '50px'],
    ])
    expect(slots().every((slot) => slot.classList.contains('overflow-hidden'))).toBe(true)
  })

  it('never load AdSense on the TV display', () => {
    useAdSense()
    renderAt('/display/9ve4shp3k7mwa2qx')

    expect(screen.queryByRole('complementary', { name: 'Advertisement' })).toBeNull()
    expect(adScripts()).toHaveLength(0)
  })

  it('come with a footer link that opens Google’s consent choices again', async () => {
    useAdSense()
    renderAt('/')

    await userEvent.click(screen.getByRole('button', { name: 'Privacy and cookie settings' }))

    const showRevocationMessage = vi.fn()
    window.googlefc = { ...window.googlefc, showRevocationMessage }
    for (const queued of window.googlefc.callbackQueue ?? []) queued()
    expect(showRevocationMessage).toHaveBeenCalledOnce()
  })
})

describe('the Privacy page', () => {
  it('says the ad spaces are placeholders when there are no ads', () => {
    renderAt('/privacy')

    expect(screen.getByText(/The ad spaces are empty placeholders today/)).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Ads' })).toBeNull()
  })

  it('describes Google’s ads and how to change consent when the build shows them', () => {
    useAdSense()
    renderAt('/privacy')

    expect(screen.getByRole('heading', { name: 'Ads' })).toBeInTheDocument()
    expect(
      screen.getByText(/directed at children, so there are\s+no personalised ads/),
    ).toBeInTheDocument()
    expect(screen.queryByText(/empty placeholders/)).toBeNull()
    expect(screen.getByRole('link', { name: /How Google uses information/ })).toHaveAttribute(
      'href',
      'https://policies.google.com/technologies/partner-sites',
    )
  })
})

describe('ads.txt', () => {
  it('names the publisher as a direct seller, without the ca- prefix', () => {
    expect(adsTxt(client)).toBe('google.com, pub-1234567890123456, DIRECT, f08c47fec0942fa0\n')
  })

  it('refuses a malformed publisher id, so a typo fails the build', () => {
    expect(() => adsTxt('pub-1234567890123456')).toThrow(/Not an AdSense publisher id/)
    expect(() => adsTxt('ca-pub-123')).toThrow()
  })
})
